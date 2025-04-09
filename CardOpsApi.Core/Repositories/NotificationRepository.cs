// using CardOpsApi.Data.Context;
// using CardOpsApi.Data.Models;
// using CardOpsApi.Data.Abstractions;
// using Microsoft.EntityFrameworkCore;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;

// namespace CardOpsApi.Data.Repositories
// {
//     public class NotificationRepository : INotificationRepository
//     {
//         private readonly CardOpsApiDbContext _context;

//         public NotificationRepository(CardOpsApiDbContext context)
//         {
//             _context = context;
//         }

//         public async Task AddNotificationAsync(Notification notification)
//         {
//             _context.Notifications.Add(notification);
//             await _context.SaveChangesAsync();
//         }

//         public async Task<List<Notification>> GetNotificationsByUserIdAsync(int userId)
//         {
//             return await _context.Notifications
//                 .Where(n => n.ToUserId == userId)
//                 .Include(n => n.FromUser)
//                 .Include(n => n.ToUser)
//                 .OrderByDescending(n => n.CreatedAt)
//                 .ToListAsync();
//         }

//         public async Task MarkAsReadAsync(int notificationId)
//         {
//             var notification = await _context.Notifications.FindAsync(notificationId);
//             if (notification != null)
//             {
//                 notification.IsRead = true;
//                 await _context.SaveChangesAsync();
//             }
//         }

//         // Get notifications by read status (filtered by true or false)
//         public async Task<List<Notification>> GetNotificationsByReadStatusAsync(bool isRead)
//         {
//             return await _context.Notifications
//                 .Where(n => n.IsRead == isRead)
//                 .Include(n => n.FromUser)
//                 .Include(n => n.ToUser)
//                 .OrderByDescending(n => n.CreatedAt)
//                 .ToListAsync();
//         }
//     }
// }
