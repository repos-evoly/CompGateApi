using Microsoft.AspNetCore.Mvc;
using BlockingApi.Data.Abstractions;
using BlockingApi.Data.Models;
using BlockingApi.Core.Dtos;
using System.Linq;
using BlockingApi.Abstractions;

namespace BlockingApi.Endpoints
{
    public class ExternalTransactionEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var transactions = app.MapGroup("/api/external-transactions").RequireAuthorization("requireAuthUser");

            // Register the /fetch endpoint
            transactions.MapPost("/fetch", FetchExternalTransactions)
                .WithName("FetchExternalTransactions")
                .Accepts<ExternalTransactionRequestDto>("application/json")
                .Produces<List<Transaction>>(200);
        }

        // Endpoint logic to fetch transactions from the external bank API,
        // and then filter out those whose eventKey already exists in the database.
        public static async Task<IResult> FetchExternalTransactions(
            [FromServices] IExternalTransactionRepository externalTransactionRepository,
            [FromServices] ITransactionRepository transactionRepository,
            [FromBody] ExternalTransactionRequestDto requestDto)
        {
            // Convert FromDate and ToDate to the required integer format (CCYYMMDD)
            int fromDate = FormatDate(requestDto.FromDate);
            int toDate = FormatDate(requestDto.ToDate);

            Console.WriteLine($"FromDate: {fromDate}, ToDate: {toDate}");

            // Fetch external transactions from the bank API
            var externalTransactions = await externalTransactionRepository.GetExternalTransactionsAsync(
                fromDate,
                toDate,
                requestDto.Limit,
                requestDto.BranchCode,
                requestDto.LocalCCY);

            // Extract distinct event keys from the external transactions (ignoring null/empty values)
            var externalEventKeys = externalTransactions
                .Select(et => et.EventKey)
                .Where(ek => !string.IsNullOrEmpty(ek))
                .Distinct()
                .ToList();

            // Query the database to get only the event keys that already exist
            var existingEventKeys = await transactionRepository.GetExistingEventKeysAsync(externalEventKeys);

            // Filter out external transactions whose eventKey already exists locally
            var newTransactions = externalTransactions
                .Where(et => string.IsNullOrEmpty(et.EventKey) || !existingEventKeys.Contains(et.EventKey))
                .ToList();

            Console.WriteLine($"Fetched {externalTransactions.Count} external transactions, filtered to {newTransactions.Count} new transactions based on eventKey.");

            return newTransactions.Any()
                ? TypedResults.Ok(newTransactions)
                : TypedResults.NotFound("No new transactions found.");
        }

        // Helper method to format dates as integers in the required format (CCYYMMDD)
        private static int FormatDate(DateTimeOffset date)
        {
            // Format the date as CCYYMMDD (e.g., for 2024-10-01, returns 1241001)
            return int.Parse($"1{date.Year.ToString().Substring(2)}{date.Month:D2}{date.Day:D2}");
        }
    }
}
