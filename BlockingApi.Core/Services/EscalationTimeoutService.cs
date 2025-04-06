using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using BlockingApi.Data.Abstractions;
using BlockingApi.Data.Models;
using BlockingApi.Data.Context;
using Microsoft.AspNetCore.SignalR;
using BlockingApi.Hubs;

public class EscalationTimeoutService : IHostedService, IDisposable
{
    private readonly ILogger<EscalationTimeoutService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private Timer? _timerAudit;
    private Timer? _timerEscalation;
    private Timer? _timerUnblockReminder; // New timer for unblock reminders

    public EscalationTimeoutService(ILogger<EscalationTimeoutService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("EscalationTimeoutService is starting...");

        // Existing timers
        _timerAudit = new Timer(PerformAuditLog, null, TimeSpan.Zero, TimeSpan.FromMinutes(100));
        _timerEscalation = new Timer(PerformEscalationCheck, null, TimeSpan.Zero, TimeSpan.FromMinutes(30));

        // New timer for scheduled unblock notifications (e.g., every 240 minutes)
        _timerUnblockReminder = new Timer(PerformUnblockReminderCheck, null, TimeSpan.Zero, TimeSpan.FromMinutes(240));

        return Task.CompletedTask;
    }

    private void PerformUnblockReminderCheck(object? state)
    {
        using var scope = _scopeFactory.CreateScope();

        try
        {
            var context = scope.ServiceProvider.GetRequiredService<BlockingApiDbContext>();
            var notificationRepo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();

            // Find block records with a scheduled unblock date that has passed and not yet unblocked.
            var blocksToNotify = context.BlockRecords
                .Where(b => b.ScheduledUnblockDate != null &&
                            b.ScheduledUnblockDate <= DateTimeOffset.Now &&
                            b.ActualUnblockDate == null)
                .ToList();

            foreach (var block in blocksToNotify)
            {
                // Log the block for debugging purposes.
                Console.WriteLine(block);

                // Construct the notification message.
                var notification = new Notification
                {
                    // Here, using BlockedByUserId for both FromUserId and ToUserId as an example.
                    FromUserId = block.BlockedByUserId,
                    ToUserId = block.BlockedByUserId,
                    Subject = "تذكير إلغاء الحظر",
                    Message = $"كان من المقرر رفع الحظر عن العميل {block.CustomerId} في {block.ScheduledUnblockDate:yyyy-MM-dd HH:mm} بالتوقيت العالمي المنسق. يُرجى مراجعة هذا الرابط وإلغاء الحظر إن أمكن.",
                    Link = $"unblock/{block.Customer.CID}",
                };

                // Save the notification in the database.
                notificationRepo.AddNotificationAsync(notification).Wait();
                _logger.LogInformation("Sent unblock reminder notification for block record {BlockId}", block.Id);

                // Broadcast the notification to all connected clients via SignalR.
                // Since we're in a synchronous callback, use .GetAwaiter().GetResult()
                hubContext.Clients.All.SendAsync("ReceiveNotification", new
                {
                    notification.Id,
                    notification.Subject,
                    notification.Message,
                    notification.CreatedAt,
                    UserId = notification.ToUserId
                }).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending unblock reminder notifications.");
        }
    }

    private void PerformAuditLog(object? state)
    {
        using var scope = _scopeFactory.CreateScope();

        try
        {
            _logger.LogInformation("EscalationTimeoutService AuditLog timer fired...");

            var auditLogRepo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

            var log = new AuditLog
            {
                UserId = 1,  // Ensure user #1 exists
                Action = "EscalationTimeoutService Heartbeat",
                Timestamp = DateTimeOffset.Now
            };

            auditLogRepo.AddAuditLog(log).Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding AuditLog in EscalationTimeoutService.");
        }
    }

    private void PerformEscalationCheck(object? state)
    {
        using var scope = _scopeFactory.CreateScope();

        try
        {
            _logger.LogInformation("EscalationTimeoutService Escalation Timer fired...");

            var transactionRepo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
            var flowRepo = scope.ServiceProvider.GetRequiredService<ITransactionFlowRepository>();

            var escalatedTransactions = transactionRepo.GetEscalatedTransactionsAsync().Result;

            foreach (var transaction in escalatedTransactions)
            {
                var flows = flowRepo.GetTransactionFlowByTransactionIdAsync(transaction.Id).Result;
                var escalationFlow = flows.FirstOrDefault(tf => tf.Action == "Escalated" && tf.CanReturn);

                if (escalationFlow != null && escalationFlow.ActionDate.AddHours(4) < DateTimeOffset.Now)
                {
                    _logger.LogInformation("Transaction {TxId} escalated for >4 hours. Returning to escalator.", transaction.Id);
                    transaction.Status = "Pending";
                    transaction.CurrentPartyUserId = transaction.InitiatorUserId;
                    transactionRepo.UpdateTransactionAsync(transaction).Wait();

                    escalationFlow.Action = "Returned";
                    escalationFlow.CanReturn = false;
                    flowRepo.UpdateTransactionFlowAsync(escalationFlow).Wait();
                }
            }

            _logger.LogInformation("Escalation check completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing escalation check in EscalationTimeoutService.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("EscalationTimeoutService is stopping...");

        _timerAudit?.Change(Timeout.Infinite, 0);
        _timerEscalation?.Change(Timeout.Infinite, 0);
        _timerUnblockReminder?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timerAudit?.Dispose();
        _timerEscalation?.Dispose();
        _timerUnblockReminder?.Dispose();
    }
}
