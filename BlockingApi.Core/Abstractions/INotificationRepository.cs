using System.Collections.Generic;
using System.Threading.Tasks;
using BlockingApi.Data.Models;

namespace BlockingApi.Data.Abstractions
{
    public interface INotificationRepository
    {
        // Add a new notification
        Task AddNotificationAsync(Notification notification);

        // Get notifications by userId
        Task<List<Notification>> GetNotificationsByUserIdAsync(int userId);

        // Mark a notification as read
        Task MarkAsReadAsync(int notificationId);
    }
}
