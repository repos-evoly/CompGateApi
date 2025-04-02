using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using BlockingApi.Abstractions;
using BlockingApi.Core.Abstractions;
using BlockingApi.Data.Abstractions;
using BlockingApi.Data.Models;
using System.Linq;

public class NotificationEndpoints : IEndpoints
{
    public void RegisterEndpoints(WebApplication app)
    {
        var notifications = app.MapGroup("/api/notifications").RequireAuthorization("requireAuthUser");

        // GET notifications for the authenticated user
        notifications.MapGet("/", GetNotifications)
            .Produces<List<NotificationDto>>(200);

        // Mark a notification as read
        notifications.MapPost("/mark-as-read/{notificationId:int}", MarkAsRead)
            .Produces(200)
            .Produces(400);

        // GET notifications for the authenticated user filtered by read status
        notifications.MapGet("/filter/{readStatus}", GetFilteredNotifications)
            .Produces<List<NotificationDto>>(200);
    }

    // Get all notifications for the authenticated user
    public static async Task<IResult> GetNotifications(
        [FromServices] INotificationRepository notificationRepository,
        HttpContext context)
    {
        int userId = GetUserIdFromClaims(context);
        var notifications = await notificationRepository.GetNotificationsByUserIdAsync(userId);

        var notificationDtos = notifications.Select(n => new NotificationDto
        {
            Id = n.Id,  // Ensure Id is included
            FromUserId = n.FromUserId,
            FromUserName = n.FromUser?.FirstName ?? "Unknown",
            ToUserId = n.ToUserId,
            ToUserName = n.ToUser?.FirstName ?? "Unknown",
            Subject = n.Subject,
            Message = n.Message,
            Link = n.Link,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }).ToList();

        return Results.Ok(notificationDtos);
    }

    // Mark a notification as read
    public static async Task<IResult> MarkAsRead(
        int notificationId,
        [FromServices] INotificationRepository notificationRepository)
    {
        await notificationRepository.MarkAsReadAsync(notificationId);
        return Results.Ok("Notification marked as read.");
    }

    // Get notifications for the authenticated user filtered by read status
    public static async Task<IResult> GetFilteredNotifications(
        string readStatus,
        [FromServices] INotificationRepository notificationRepository,
        HttpContext context)
    {
        // Convert readStatus to boolean (assumes "read" returns true, anything else returns false)
        bool isRead = readStatus.ToLower() == "read";

        int userId = GetUserIdFromClaims(context);
        // First, get all notifications for the user
        var notifications = await notificationRepository.GetNotificationsByUserIdAsync(userId);
        // Then filter by read status
        notifications = notifications.Where(n => n.IsRead == isRead).ToList();

        var notificationDtos = notifications.Select(n => new NotificationDto
        {
            Id = n.Id,  // Ensure Id is included
            FromUserId = n.FromUserId,
            FromUserName = n.FromUser?.FirstName ?? "Unknown",
            ToUserId = n.ToUserId,
            ToUserName = n.ToUser?.FirstName ?? "Unknown",
            Subject = n.Subject,
            Message = n.Message,
            Link = n.Link,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }).ToList();

        return Results.Ok(notificationDtos);
    }

    // Helper method to extract the authenticated user's ID from the HttpContext claims
    private static int GetUserIdFromClaims(HttpContext context)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out int userId) ? userId : 0;
    }
}
