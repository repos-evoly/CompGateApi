using Microsoft.AspNetCore.Mvc;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Abstractions;
using CompGateApi.Data.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using CompGateApi.Hubs;

public class NotificationEndpoints : IEndpoints
{
    public void RegisterEndpoints(WebApplication app)
    {
        var notifications = app.MapGroup("/api/notifications").RequireAuthorization("requireAuthUser");

        // GET endpoint (existing)
        notifications.MapGet("/", GetNotifications)
            .Produces<List<NotificationDto>>(200);

        // Mark a notification as read (existing)
        notifications.MapPost("/mark-as-read/{notificationId:int}", MarkAsRead)
            .Produces(200)
            .Produces(400);

        // New endpoint: Send notification and broadcast to all connected clients.
        notifications.MapPost("/send", SendNotification)
           .Produces(200)
           .Produces(400);
    }

    // GET endpoint: retrieves notifications for the authenticated user.
    public static async Task<IResult> GetNotifications(
        [FromServices] INotificationRepository notificationRepository,
        HttpContext context,
        [FromQuery] string? readStatus)
    {
        int userId = GetUserIdFromClaims(context);
        var notifications = await notificationRepository.GetNotificationsByUserIdAsync(userId);

        // If a readStatus is provided, filter the notifications accordingly.
        if (!string.IsNullOrEmpty(readStatus))
        {
            string normalizedStatus = readStatus.ToLower();
            if (normalizedStatus == "read")
            {
                notifications = notifications.Where(n => n.IsRead).ToList();
            }
            else if (normalizedStatus == "unread")
            {
                notifications = notifications.Where(n => !n.IsRead).ToList();
            }
        }

        var notificationDtos = notifications.Select(n => new NotificationDto
        {
            Id = n.Id,
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

    // POST endpoint: marks a notification as read.
    public static async Task<IResult> MarkAsRead(
        int notificationId,
        [FromServices] INotificationRepository notificationRepository)
    {
        await notificationRepository.MarkAsReadAsync(notificationId);
        return Results.Ok("Notification marked as read.");
    }

    // POST endpoint: Sends a notification and broadcasts it to all connected clients.
    public static async Task<IResult> SendNotification(
       [FromBody] NotificationSendDto notificationDto,
       [FromServices] INotificationRepository notificationRepository,
       [FromServices] IHubContext<NotificationHub> hubContext)
    {
        // Map the DTO to your Notification model.
        var notification = new Notification
        {
            FromUserId = notificationDto.FromUserId,
            ToUserId = notificationDto.ToUserId,
            Subject = notificationDto.Subject,
            Message = notificationDto.Message,
            Link = notificationDto.Link,
            IsRead = false,
            CreatedAt = DateTimeOffset.Now
        };

        // Save the notification to the database.
        await notificationRepository.AddNotificationAsync(notification);

        // Broadcast the notification to all connected clients.
        await hubContext.Clients.All.SendAsync("ReceiveNotification", new
        {
            NotificationId = notification.Id,
            NotificationSubject = notification.Subject,
            NotificationMessage = notification.Message,
            Created = notification.CreatedAt,
            UserId = notification.ToUserId  // Added user id field

        });

        return Results.Ok("Notification sent successfully.");
    }

    // Helper method to extract the authenticated user's ID from the HttpContext claims.
    private static int GetUserIdFromClaims(HttpContext context)
    {
        var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out int userId) ? userId : 0;
    }
}
