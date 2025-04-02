using BlockingApi.Abstractions;
using BlockingApi.Core.Abstractions;
using BlockingApi.Core.Dtos;
using BlockingApi.Data.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace BlockingApi.Endpoints
{
    public class DashboardEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var dashboard = app.MapGroup("/api/dashboard").RequireAuthorization("requireAuthUser");

            // Register the endpoint to fetch the dashboard statistics
            dashboard.MapGet("/overview", GetDashboardStats)
                .Produces<DashboardStatsDto>(200)
                .Produces(400);
        }
        public static async Task<IResult> GetDashboardStats(
            [FromServices] ITransactionRepository transactionRepository,
            [FromServices] ICustomerRepository customerRepository,
            ILogger<DashboardEndpoints> logger)
        {
            // Fetch the statistics for the dashboard

            var blockedAccounts = await customerRepository.GetBlockedAccountsCountAsync();
            var flaggedTransactions = await transactionRepository.GetFlaggedTransactionsCountAsync();
            var blockedUsersToday = await customerRepository.GetBlockedUsersTodayCountAsync();
            var highValueTransactions = await transactionRepository.GetHighValueTransactionsCountAsync();

            // Create the response DTO
            var stats = new DashboardStatsDto
            {
                BlockedAccounts = blockedAccounts,
                FlaggedTransactions = flaggedTransactions,
                BlockedUsersToday = blockedUsersToday,
                HighValueTransactions = highValueTransactions
            };

            return Results.Ok(stats);
        }
    }
}
