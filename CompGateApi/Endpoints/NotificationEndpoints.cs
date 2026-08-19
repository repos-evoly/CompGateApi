using System.Security.Claims;
using CompGateApi.Abstractions;
using CompGateApi.Core.Authentication;
using CompGateApi.Core.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace CompGateApi.Endpoints;

public sealed class NotificationEndpoints : IEndpoints
{
    public void RegisterEndpoints(WebApplication app)
    {
        // Existing web clients keep their user-JWT route. It is now ownership scoped.
        var web = app.MapGroup("/api/notifications")
            .RequireAuthorization("requireAuthUser")
            .WithTags("Notifications");
        web.MapGet("/", GetWebNotifications);
        web.MapPost("/mark-as-read/{notificationId:int}", MarkWebNotificationRead);

        // The Mobile BFF must present both the user JWT and its service JWT.
        var mobile = app.MapGroup("/api/mobile-notifications")
            .RequireAuthorization(
                MobileServiceAuthenticationDefaults.RequireCompanyUserAndMobileBffServicePolicy)
            .WithTags("Mobile Notifications");
        mobile.MapGet("", GetMobileNotifications);
        mobile.MapGet("/unread-count", GetUnreadCount);
        mobile.MapGet("/{notificationId:int}", GetMobileNotification);
        mobile.MapPost("/{notificationId:int}/read", MarkMobileNotificationRead);

        // Service-only delivery endpoints. They are not generic proxy/send routes.
        var deliveries = app.MapGroup("/api/mobile-notification-deliveries")
            .RequireAuthorization(
                MobileServiceAuthenticationDefaults.RequireMobileBffServicePolicy)
            .WithTags("Mobile Notification Delivery");
        deliveries.MapPost("/claim", ClaimDeliveries);
        deliveries.MapPost("/{eventId:guid}/complete", CompleteDelivery);
        deliveries.MapPost("/{eventId:guid}/fail", FailDelivery);
    }

    private static async Task<IResult> GetWebNotifications(
        HttpContext context,
        ICompanyNotificationService service,
        [FromQuery] string? readStatus,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthUserId(context.User, out var authUserId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var result = await service.GetInboxAsync(
                authUserId,
                1,
                100,
                readStatus ?? "all",
                cancellationToken);
            return Results.Ok(result.Data);
        }
        catch (InvalidNotificationQueryException)
        {
            return Problem(400, "invalid_notification_status", "Status must be all, read, or unread.");
        }
    }

    private static async Task<IResult> MarkWebNotificationRead(
        int notificationId,
        HttpContext context,
        ICompanyNotificationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthUserId(context.User, out var authUserId))
        {
            return Results.Unauthorized();
        }

        return await service.MarkReadAsync(authUserId, notificationId, cancellationToken)
            ? Results.Ok(new { success = true })
            : Results.NotFound();
    }

    private static async Task<IResult> GetMobileNotifications(
        HttpContext context,
        ICompanyNotificationService service,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string status = "all")
    {
        if (!TryGetAuthUserId(context.User, out var authUserId))
        {
            return Problem(401, "user_authentication_required", "A valid company user is required.");
        }

        try
        {
            return Results.Ok(await service.GetInboxAsync(
                authUserId,
                page,
                limit,
                status,
                context.RequestAborted));
        }
        catch (InvalidNotificationQueryException)
        {
            return Problem(400, "invalid_notification_status", "Status must be all, read, or unread.");
        }
    }

    private static async Task<IResult> GetMobileNotification(
        int notificationId,
        HttpContext context,
        ICompanyNotificationService service)
    {
        if (!TryGetAuthUserId(context.User, out var authUserId))
        {
            return Problem(401, "user_authentication_required", "A valid company user is required.");
        }

        var notification = await service.GetAsync(
            authUserId,
            notificationId,
            context.RequestAborted);
        return notification is null ? Results.NotFound() : Results.Ok(notification);
    }

    private static async Task<IResult> MarkMobileNotificationRead(
        int notificationId,
        HttpContext context,
        ICompanyNotificationService service)
    {
        if (!TryGetAuthUserId(context.User, out var authUserId))
        {
            return Problem(401, "user_authentication_required", "A valid company user is required.");
        }

        return await service.MarkReadAsync(
            authUserId,
            notificationId,
            context.RequestAborted)
            ? Results.Ok(new { notificationId, isRead = true })
            : Results.NotFound();
    }

    private static async Task<IResult> GetUnreadCount(
        HttpContext context,
        ICompanyNotificationService service)
    {
        if (!TryGetAuthUserId(context.User, out var authUserId))
        {
            return Problem(401, "user_authentication_required", "A valid company user is required.");
        }

        return Results.Ok(new
        {
            unreadCount = await service.GetUnreadCountAsync(
                authUserId,
                context.RequestAborted)
        });
    }

    private static async Task<IResult> ClaimDeliveries(
        NotificationClaimRequest request,
        ICompanyNotificationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.ClaimAsync(
                request.WorkerId,
                request.BatchSize,
                request.LeaseSeconds,
                cancellationToken));
        }
        catch (InvalidNotificationDeliveryRequestException)
        {
            return Problem(400, "invalid_delivery_claim", "The notification claim request is invalid.");
        }
    }

    private static async Task<IResult> CompleteDelivery(
        Guid eventId,
        NotificationCompletionRequest request,
        ICompanyNotificationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return await service.CompleteAsync(
                eventId,
                request.WorkerId,
                cancellationToken)
                ? Results.Ok(new { eventId, completed = true })
                : Results.Conflict(new { code = "delivery_lease_conflict" });
        }
        catch (InvalidNotificationDeliveryRequestException)
        {
            return Problem(400, "invalid_delivery_completion", "The delivery completion request is invalid.");
        }
    }

    private static async Task<IResult> FailDelivery(
        Guid eventId,
        NotificationFailureRequest request,
        ICompanyNotificationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.FailAsync(
                eventId,
                request.WorkerId,
                request.Terminal,
                request.RetryAfterSeconds,
                request.FailureCategory,
                cancellationToken);
            return result is null
                ? Results.Conflict(new { code = "delivery_lease_conflict" })
                : Results.Ok(result);
        }
        catch (InvalidNotificationDeliveryRequestException)
        {
            return Problem(400, "invalid_delivery_failure", "The delivery failure request is invalid.");
        }
    }

    private static bool TryGetAuthUserId(ClaimsPrincipal principal, out int authUserId)
    {
        var value = principal.FindFirst("nameid")?.Value ??
                    principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out authUserId) && authUserId > 0;
    }

    private static IResult Problem(int status, string code, string detail) =>
        Results.Problem(
            statusCode: status,
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}

public sealed record NotificationClaimRequest(
    string WorkerId,
    int BatchSize = 20,
    int LeaseSeconds = 120);

public sealed record NotificationCompletionRequest(string WorkerId);

public sealed record NotificationFailureRequest(
    string WorkerId,
    bool Terminal,
    int RetryAfterSeconds,
    string FailureCategory);
