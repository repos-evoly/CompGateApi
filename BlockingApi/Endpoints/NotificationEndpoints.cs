using BlockingApi.Abstractions;
using BlockingApi.Data.Abstractions;
using Microsoft.AspNetCore.Mvc;

public class NotificationEndpoints : IEndpoints
{
    public void RegisterEndpoints(WebApplication app)
    {
        var notifications = app.MapGroup("/api/notifications").RequireAuthorization("requireAuthUser");

        notifications.MapGet("/user/{userId}", GetNotificationsForUser)
            .Produces<List<NotificationDto>>(200);
    }

    // Get all notifications for a user
    public static async Task<IResult> GetNotificationsForUser(
        int userId,
        [FromServices] INotificationRepository notificationRepository)
    {
        var notifications = await notificationRepository.GetNotificationsByUserIdAsync(userId);

        var notificationDtos = notifications.Select(n => new NotificationDto
        {
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
}
