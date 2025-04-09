using CardOpsApi.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CardOpsApi.Data.Abstractions
{
    public interface INotificationRepository
    {
        // Add a new notification
        Task AddNotificationAsync(Notification notification);

        // Get notifications by userId
        Task<List<Notification>> GetNotificationsByUserIdAsync(int userId);

        // Mark a notification as read
        Task MarkAsReadAsync(int notificationId);

        // Get notifications by read status
        Task<List<Notification>> GetNotificationsByReadStatusAsync(bool isRead);
    }
}
