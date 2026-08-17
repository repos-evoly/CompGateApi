using System.Data;
using System.Linq.Expressions;
using System.Text.Json;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CompGateApi.Core.Notifications;

public sealed class CompanyNotificationService(
    CompGateApiDbContext db,
    IOptionsMonitor<NotificationDeliveryOptions> options,
    TimeProvider timeProvider) : ICompanyNotificationService, INotificationEventWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Expression<Func<Notification, MobileNotificationDto>> InboxProjection =
        item => new MobileNotificationDto(
            item.Id,
            item.Type,
            item.Subject,
            item.Message,
            item.EntityType,
            item.EntityId,
            item.ReadAt != null,
            item.ReadAt,
            item.CreatedAt,
            item.ExpiresAt,
            item.FromUser == null
                ? null
                : (item.FromUser.FirstName + " " + item.FromUser.LastName).Trim());
    private static readonly string[] ApproverRoles =
    [
        "Checker",
        "GeneralChecker",
        "CompanyManager",
        "Admin",
        "SuperAdmin"
    ];

    public async Task<PagedResult<MobileNotificationDto>> GetInboxAsync(
        int authUserId,
        int page,
        int limit,
        string status,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);
        status = NormalizeStatus(status);

        var query = db.Notifications
            .AsNoTracking()
            .Where(item => item.ToAuthUserId == authUserId);
        query = status switch
        {
            "read" => query.Where(item => item.ReadAt != null),
            "unread" => query.Where(item => item.ReadAt == null),
            _ => query
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(InboxProjection)
            .ToListAsync(cancellationToken);

        return new PagedResult<MobileNotificationDto>
        {
            Data = items,
            Page = page,
            Limit = limit,
            TotalRecords = total,
            TotalPages = (int)Math.Ceiling(total / (double)limit)
        };
    }

    public Task<MobileNotificationDto?> GetAsync(
        int authUserId,
        int notificationId,
        CancellationToken cancellationToken) =>
        db.Notifications
            .AsNoTracking()
            .Where(item => item.Id == notificationId && item.ToAuthUserId == authUserId)
            .Select(InboxProjection)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> MarkReadAsync(
        int authUserId,
        int notificationId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var updated = await db.Notifications
            .Where(item => item.Id == notificationId && item.ToAuthUserId == authUserId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.IsRead, true)
                    .SetProperty(item => item.ReadAt, item => item.ReadAt ?? now)
                    .SetProperty(item => item.UpdatedAt, now),
                cancellationToken);
        return updated == 1;
    }

    public Task<int> GetUnreadCountAsync(
        int authUserId,
        CancellationToken cancellationToken) =>
        db.Notifications.CountAsync(
            item => item.ToAuthUserId == authUserId && item.ReadAt == null,
            cancellationToken);

    public async Task<IReadOnlyList<NotificationDeliveryEventDto>> ClaimAsync(
        string workerId,
        int batchSize,
        int leaseSeconds,
        CancellationToken cancellationToken)
    {
        workerId = NormalizeWorkerId(workerId);
        var settings = options.CurrentValue;
        batchSize = Math.Clamp(batchSize, 1, Math.Clamp(settings.MaximumClaimBatchSize, 1, 500));
        leaseSeconds = Math.Clamp(leaseSeconds, 15, Math.Clamp(settings.MaximumLeaseSeconds, 15, 900));
        var now = timeProvider.GetUtcNow();
        var leaseExpiresAt = now.AddSeconds(leaseSeconds);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var events = await db.NotificationOutbox
            .Where(item =>
                item.CompletedAt == null &&
                item.DeadLetteredAt == null &&
                item.AvailableAt <= now &&
                (item.LeaseExpiresAt == null || item.LeaseExpiresAt <= now))
            .OrderBy(item => item.AvailableAt)
            .ThenBy(item => item.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var item in events)
        {
            item.LeaseOwner = workerId;
            item.LeaseExpiresAt = leaseExpiresAt;
            item.AttemptCount++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return events.Select(item => MapDelivery(item, leaseExpiresAt)).ToArray();
    }

    public async Task<bool> CompleteAsync(
        Guid eventId,
        string workerId,
        CancellationToken cancellationToken)
    {
        workerId = NormalizeWorkerId(workerId);
        var item = await db.NotificationOutbox.FirstOrDefaultAsync(
            outbox => outbox.EventId == eventId,
            cancellationToken);
        if (item is null)
        {
            return false;
        }

        if (item.CompletedAt is not null)
        {
            return true;
        }

        if (!string.Equals(item.LeaseOwner, workerId, StringComparison.Ordinal))
        {
            return false;
        }

        item.CompletedAt = timeProvider.GetUtcNow();
        item.LeaseOwner = null;
        item.LeaseExpiresAt = null;
        item.LastFailureCategory = null;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<NotificationDeliveryFailureDto?> FailAsync(
        Guid eventId,
        string workerId,
        bool terminal,
        int retryAfterSeconds,
        string failureCategory,
        CancellationToken cancellationToken)
    {
        workerId = NormalizeWorkerId(workerId);
        var item = await db.NotificationOutbox.FirstOrDefaultAsync(
            outbox => outbox.EventId == eventId,
            cancellationToken);
        if (item is null || item.CompletedAt is not null ||
            !string.Equals(item.LeaseOwner, workerId, StringComparison.Ordinal))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var maximumAttempts = Math.Clamp(options.CurrentValue.MaximumAttempts, 1, 100);
        var deadLettered = terminal || item.AttemptCount >= maximumAttempts;
        item.LastFailureCategory = NormalizeFailureCategory(failureCategory);
        item.LeaseOwner = null;
        item.LeaseExpiresAt = null;
        if (deadLettered)
        {
            item.DeadLetteredAt = now;
        }
        else
        {
            item.AvailableAt = now.AddSeconds(Math.Clamp(retryAfterSeconds, 5, 3600));
        }

        await db.SaveChangesAsync(cancellationToken);
        return new NotificationDeliveryFailureDto(
            eventId,
            deadLettered,
            item.AttemptCount,
            deadLettered ? null : item.AvailableAt);
    }

    public async Task<int> CreateForCompanyApproversAsync(
        int companyId,
        int? actorUserId,
        string type,
        string entityType,
        string entityId,
        string subject,
        string message,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var recipients = await db.Users
            .AsNoTracking()
            .Where(user =>
                user.CompanyId == companyId &&
                user.IsActive &&
                user.AuthUserId > 0 &&
                user.Id != actorUserId &&
                (user.IsCompanyAdmin || ApproverRoles.Contains(user.Role.NameLT)))
            .Select(user => new Recipient(user.Id, user.AuthUserId, user.CompanyId))
            .ToArrayAsync(cancellationToken);

        return await CreateAsync(
            recipients,
            actorUserId,
            type,
            entityType,
            entityId,
            subject,
            message,
            idempotencyKey,
            cancellationToken);
    }

    public async Task<bool> CreateForUserAsync(
        int recipientUserId,
        int? actorUserId,
        string type,
        string entityType,
        string entityId,
        string subject,
        string message,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var recipient = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == recipientUserId && user.IsActive && user.AuthUserId > 0)
            .Select(user => new Recipient(user.Id, user.AuthUserId, user.CompanyId))
            .FirstOrDefaultAsync(cancellationToken);
        if (recipient is null)
        {
            return false;
        }

        return await CreateAsync(
            [recipient],
            actorUserId,
            type,
            entityType,
            entityId,
            subject,
            message,
            idempotencyKey,
            cancellationToken) == 1;
    }

    private async Task<int> CreateAsync(
        IReadOnlyCollection<Recipient> recipients,
        int? actorUserId,
        string type,
        string entityType,
        string entityId,
        string subject,
        string message,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (recipients.Count == 0)
        {
            return 0;
        }

        ValidateNotification(type, entityType, entityId, subject, message, idempotencyKey);
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var now = timeProvider.GetUtcNow();
        var existingKeys = await db.NotificationOutbox
            .AsNoTracking()
            .Where(item => item.IdempotencyKey.StartsWith(idempotencyKey + ":"))
            .Select(item => item.IdempotencyKey)
            .ToListAsync(cancellationToken);
        var existing = existingKeys.ToHashSet(StringComparer.Ordinal);
        var pending = recipients
            .Where(recipient => !existing.Contains($"{idempotencyKey}:{recipient.AuthUserId}"))
            .Select(recipient => new
            {
                Recipient = recipient,
                EventId = Guid.NewGuid(),
                Key = $"{idempotencyKey}:{recipient.AuthUserId}"
            })
            .ToArray();
        if (pending.Length == 0)
        {
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return 0;
        }

        var notifications = pending.Select(item => new Notification
        {
            EventId = item.EventId,
            FromUserId = actorUserId,
            ToUserId = item.Recipient.UserId,
            ToAuthUserId = item.Recipient.AuthUserId,
            CompanyId = item.Recipient.CompanyId,
            Type = type.Trim(),
            Subject = subject.Trim(),
            Message = message.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId.Trim(),
            Link = BuildSafeLink(entityType, entityId),
            IsRead = false
        }).ToArray();
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var item in pending)
        {
            var notification = notifications.Single(value => value.EventId == item.EventId);
            var payload = new NotificationPayload(
                notification.Subject,
                notification.Message,
                new Dictionary<string, string>
                {
                    ["notificationId"] = notification.Id.ToString(),
                    ["type"] = notification.Type,
                    ["entityType"] = notification.EntityType,
                    ["entityId"] = notification.EntityId,
                    ["eventId"] = notification.EventId.ToString("N")
                });
            db.NotificationOutbox.Add(new NotificationOutbox
            {
                EventId = item.EventId,
                IdempotencyKey = item.Key,
                NotificationId = notification.Id,
                RecipientAuthUserId = notification.ToAuthUserId,
                EventType = notification.Type,
                EventVersion = 1,
                PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
                CreatedAt = now,
                AvailableAt = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return pending.Length;
    }

    private static NotificationDeliveryEventDto MapDelivery(
        NotificationOutbox item,
        DateTimeOffset leaseExpiresAt)
    {
        var payload = JsonSerializer.Deserialize<NotificationPayload>(item.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("The notification outbox payload is invalid.");
        return new NotificationDeliveryEventDto(
            item.EventId,
            item.RecipientAuthUserId,
            item.EventType,
            item.EventVersion,
            payload.Title,
            payload.Body,
            payload.Data,
            item.AttemptCount,
            leaseExpiresAt);
    }

    private static string NormalizeStatus(string? value) =>
        (value ?? "all").Trim().ToLowerInvariant() switch
        {
            "all" => "all",
            "read" => "read",
            "unread" => "unread",
            _ => throw new InvalidNotificationQueryException()
        };

    private static string NormalizeWorkerId(string? value)
    {
        var workerId = value?.Trim() ?? string.Empty;
        if (workerId.Length is < 3 or > 100 || workerId.Any(char.IsControl))
        {
            throw new InvalidNotificationDeliveryRequestException();
        }
        return workerId;
    }

    private static string NormalizeFailureCategory(string? value)
    {
        var category = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return category.Length is >= 1 and <= 100 &&
               category.All(character => char.IsLetterOrDigit(character) || character is '_' or '-')
            ? category
            : "unspecified";
    }

    private static void ValidateNotification(
        string type,
        string entityType,
        string entityId,
        string subject,
        string message,
        string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(type) || type.Length > 64 ||
            string.IsNullOrWhiteSpace(entityType) || entityType.Length > 64 ||
            string.IsNullOrWhiteSpace(entityId) || entityId.Length > 128 ||
            string.IsNullOrWhiteSpace(subject) || subject.Length > 200 ||
            string.IsNullOrWhiteSpace(message) || message.Length > 1000 ||
            string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 150)
        {
            throw new InvalidNotificationEventException();
        }
    }

    private static string BuildSafeLink(string entityType, string entityId) =>
        entityType.Trim().ToLowerInvariant() switch
        {
            "transfer" => $"/transfers/{Uri.EscapeDataString(entityId.Trim())}",
            "salary" => $"/salaries/{Uri.EscapeDataString(entityId.Trim())}",
            _ => string.Empty
        };

    private sealed record Recipient(int UserId, int AuthUserId, int? CompanyId);
}

public sealed class InvalidNotificationQueryException : Exception;
public sealed class InvalidNotificationDeliveryRequestException : Exception;
public sealed class InvalidNotificationEventException : Exception;
