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

        // Endpoint logic to fetch transactions from the external bank API
        public static async Task<IResult> FetchExternalTransactions([FromServices] IExternalTransactionRepository externalTransactionRepository, [FromBody] ExternalTransactionRequestDto requestDto)
        {
            // Convert FromDate and ToDate to the required integer format (CCYYMMDD)
            int fromDate = FormatDate(requestDto.FromDate);
            int toDate = FormatDate(requestDto.ToDate);

            // Log the formatted dates for debugging
            Console.WriteLine($"FromDate: {fromDate}, ToDate: {toDate}");

            var transactions = await externalTransactionRepository.GetExternalTransactionsAsync(
                fromDate,
                toDate,
                requestDto.Limit,
                requestDto.BranchCode,
                requestDto.LocalCCY);

            // Return transactions or a 404 if not found
            if (transactions.Any())
            {
                return TypedResults.Ok(transactions);
            }
            else
            {
                return TypedResults.NotFound("No transactions found.");
            }
        }

        // Helper method to format dates as integers in the required format (CCYYMMDD)
        private static int FormatDate(DateTime date)
        {
            // Format the date as CCYYMMDD (e.g., 1241001 for 2024-10-01)
            return int.Parse($"1{date.Year.ToString().Substring(2)}{date.Month:D2}{date.Day:D2}");
        }
    }
}
