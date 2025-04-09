// using Microsoft.AspNetCore.SignalR;
// using CardOpsApi.Data.Abstractions;
// using CardOpsApi.Data.Models;
// using System.Threading.Tasks;
// using CardOpsApi.Hubs;

// namespace CardOpsApi.Core.Services
// {
//     public class NotificationService
//     {
//         private readonly INotificationRepository _notificationRepository;
//         private readonly IHubContext<NotificationHub> _hubContext;

//         public NotificationService(INotificationRepository notificationRepository, IHubContext<NotificationHub> hubContext)
//         {
//             _notificationRepository = notificationRepository;
//             _hubContext = hubContext;
//         }

//         // This method adds a notification and then pushes a message to the user.
//         public async Task AddNotificationAndPushAsync(Notification notification)
//         {
//             await _notificationRepository.AddNotificationAsync(notification);

//             // Push the notification message to the target user.
//             await _hubContext.Clients.User(notification.ToUserId.ToString())
//                 .SendAsync("ReceiveNotification", new
//                 {
//                     notification.Id,
//                     notification.Subject,
//                     notification.Message,
//                     notification.CreatedAt
//                 });
//         }
//     }
// }
