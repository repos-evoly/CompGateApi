using CompGateApi.Core.Dtos;

namespace CompGateApi.Core.Notifications;

public sealed class NotificationDeliveryOptions
{
    public const string SectionName = "NotificationDelivery";

    public int MaximumClaimBatchSize { get; init; } = 100;
    public int MaximumAttempts { get; init; } = 10;
    public int MaximumLeaseSeconds { get; init; } = 300;
}

public interface ICompanyNotificationService
{
    Task<PagedResult<MobileNotificationDto>> GetInboxAsync(
        int authUserId,
        int page,
        int limit,
        string status,
        CancellationToken cancellationToken);

    Task<MobileNotificationDto?> GetAsync(
        int authUserId,
        int notificationId,
        CancellationToken cancellationToken);

    Task<bool> MarkReadAsync(
        int authUserId,
        int notificationId,
        CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(int authUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationDeliveryEventDto>> ClaimAsync(
        string workerId,
        int batchSize,
        int leaseSeconds,
        CancellationToken cancellationToken);

    Task<bool> CompleteAsync(
        Guid eventId,
        string workerId,
        CancellationToken cancellationToken);

    Task<NotificationDeliveryFailureDto?> FailAsync(
        Guid eventId,
        string workerId,
        bool terminal,
        int retryAfterSeconds,
        string failureCategory,
        CancellationToken cancellationToken);
}

public interface INotificationEventWriter
{
    Task<int> CreateForCompanyApproversAsync(
        int companyId,
        int? actorUserId,
        string type,
        string entityType,
        string entityId,
        string subject,
        string message,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<bool> CreateForUserAsync(
        int recipientUserId,
        int? actorUserId,
        string type,
        string entityType,
        string entityId,
        string subject,
        string message,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

public sealed record MobileNotificationDto(
    int Id,
    string Type,
    string Subject,
    string Message,
    string EntityType,
    string EntityId,
    bool IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    string? FromUserName);

public sealed record NotificationDeliveryEventDto(
    Guid EventId,
    int RecipientAuthUserId,
    string EventType,
    int EventVersion,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Data,
    int AttemptCount,
    DateTimeOffset LeaseExpiresAt);

public sealed record NotificationDeliveryFailureDto(
    Guid EventId,
    bool DeadLettered,
    int AttemptCount,
    DateTimeOffset? AvailableAt);

public sealed record NotificationPayload(
    string Title,
    string Body,
    Dictionary<string, string> Data);
