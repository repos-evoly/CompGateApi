using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using CardOpsApi.Core.Abstractions;
using CardOpsApi.Core.Dtos;
using CardOpsApi.Data.Models;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CardOpsApi.Abstractions;
using System.Security.Claims;

namespace CardOpsApi.Endpoints
{
    public class TransactionEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            // Require a specific policy for transactions – adjust the policy name as needed.
            var transactions = app.MapGroup("/api/transactions").RequireAuthorization("requireAuthUser");

            transactions.MapGet("/", GetTransactions)
           .WithName("GetTransactions")
           .Produces<List<TransactionDto>>(200);

            transactions.MapGet("/{id:int}", GetTransactionById)
                .WithName("GetTransactionById")
                .Produces<TransactionDto>(200)
                .Produces(404);

            transactions.MapPost("/", CreateTransaction)
                .WithName("CreateTransaction")
                .Accepts<TransactionCreateDto>("application/json")
                .Produces<TransactionDto>(201)
                .Produces(400);

            transactions.MapPut("/{id:int}", UpdateTransaction)
                .WithName("UpdateTransaction")
                .Accepts<TransactionUpdateDto>("application/json")
                .Produces<TransactionDto>(200)
                .Produces(400)
                .Produces(404);

            transactions.MapDelete("/{id:int}", DeleteTransaction)
                .WithName("DeleteTransaction")
                .Produces(200)
                .Produces(404);

            // New endpoint: GET /api/transactions/summary
            transactions.MapGet("/summary", GetTransactionSummary)
                .WithName("GetTransactionSummary")
                .Produces<TransactionSummaryDto>(200);

            // New endpoint: GET /api/transactions/top-atm-refunds
            transactions.MapGet("/top-atm-refunds", GetTopAtmRefunds)
                .WithName("GetTopAtmRefunds")
                .Produces<List<TopAtmRefundDto>>(200);

            transactions.MapGet("/top-reasons", GetTopReasons)
                .WithName("GetTopReasons")
                .Produces<List<TopReasonDto>>(200);
        }


        // GET /api/transactions?searchTerm=&searchBy=&page=&limit=
        public static async Task<IResult> GetTransactions(
             [FromServices] ITransactionRepository transactionRepository,
             [FromServices] IMapper mapper,
             [FromQuery] string? searchTerm,
             [FromQuery] string? searchBy,
             [FromQuery] string? type,
             [FromQuery] int page = 1,
             [FromQuery] int limit = 10)
        {
            var transactions = await transactionRepository.GetAllAsync(searchTerm, searchBy, type, page, limit);
            var transactionDtos = mapper.Map<List<TransactionDto>>(transactions);
            return Results.Ok(transactionDtos);
        }

        // GET /api/transactions/{id}
        public static async Task<IResult> GetTransactionById(
            int id,
            [FromServices] ITransactionRepository transactionRepository,
            [FromServices] IMapper mapper)
        {
            var transaction = await transactionRepository.GetByIdAsync(id);
            if (transaction == null)
            {
                return Results.NotFound("Transaction not found.");
            }
            var dto = mapper.Map<TransactionDto>(transaction);
            return Results.Ok(dto);
        }

        // POST /api/transactions
        public static async Task<IResult> CreateTransaction(
            [FromBody] TransactionCreateDto createDto,
            [FromServices] ITransactionRepository transactionRepository,
            [FromServices] IMapper mapper,
            [FromServices] IValidator<TransactionCreateDto> validator)
        {
            ValidationResult validationResult = await validator.ValidateAsync(createDto);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));
            }

            var transaction = mapper.Map<Transactions>(createDto);
            await transactionRepository.CreateAsync(transaction);
            var dto = mapper.Map<TransactionDto>(transaction);
            return Results.Created($"/api/transactions/{dto.Id}", dto);
        }

        // PUT /api/transactions/{id}
        public static async Task<IResult> UpdateTransaction(
            int id,
            [FromBody] TransactionUpdateDto updateDto,
            [FromServices] ITransactionRepository transactionRepository,
            [FromServices] IMapper mapper,
            [FromServices] IValidator<TransactionUpdateDto> validator)
        {
            var existingTransaction = await transactionRepository.GetByIdAsync(id);
            if (existingTransaction == null)
            {
                return Results.NotFound("Transaction not found.");
            }

            ValidationResult validationResult = await validator.ValidateAsync(updateDto);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));
            }

            // Map update DTO onto existing entity
            mapper.Map(updateDto, existingTransaction);
            await transactionRepository.UpdateAsync(existingTransaction);
            var dto = mapper.Map<TransactionDto>(existingTransaction);
            return Results.Ok(dto);
        }

        // DELETE /api/transactions/{id}
        public static async Task<IResult> DeleteTransaction(
            int id,
            [FromServices] ITransactionRepository transactionRepository)
        {
            var existingTransaction = await transactionRepository.GetByIdAsync(id);
            if (existingTransaction == null)
            {
                return Results.NotFound("Transaction not found.");
            }
            await transactionRepository.DeleteAsync(id);
            return Results.Ok("Transaction deleted successfully.");
        }

        // New endpoint: Returns a summary for transactions
        // New endpoint: Returns a summary for transactions for a given year
        public static async Task<IResult> GetTransactionSummary(
     [FromServices] ITransactionRepository transactionRepository,
     [FromQuery] int? year)
        {
            // Retrieve all transactions (for demonstration, using int.MaxValue to fetch all records)
            var transactions = await transactionRepository.GetAllAsync(null, null, null, 1, int.MaxValue);

            // If a year is specified, filter transactions to that year.
            if (year.HasValue)
            {
                transactions = transactions.Where(t => t.Date.Year == year.Value).ToList();
            }

            // Calculate the summary using the countervalue (Amount multiplied by the Currency Rate).
            var atmCount = transactions.Count(t => t.Type.Equals("ATM", System.StringComparison.OrdinalIgnoreCase));
            var posCount = transactions.Count(t => t.Type.Equals("POS", System.StringComparison.OrdinalIgnoreCase));
            var posTotalAmount = transactions
                                    .Where(t => t.Type.Equals("POS", System.StringComparison.OrdinalIgnoreCase))
                                    .Sum(t => t.Amount * t.Currency.Rate);
            var atmTotalAmount = transactions
                                    .Where(t => t.Type.Equals("ATM", System.StringComparison.OrdinalIgnoreCase))
                                    .Sum(t => t.Amount * t.Currency.Rate);

            var summary = new TransactionSummaryDto
            {
                AtmCount = atmCount,
                PosCount = posCount,
                PosTotalAmount = posTotalAmount,
                AtmTotalAmount = atmTotalAmount
            };

            return Results.Ok(summary);
        }


        // New endpoint: Returns the top 10 ATMs with most refunds filtered by year (if provided)
        public static async Task<IResult> GetTopAtmRefunds(
             [FromServices] ITransactionRepository transactionRepository,
             [FromQuery] int? year)
        {
            // Retrieve all transactions.
            var transactions = await transactionRepository.GetAllAsync(null, null, null, 1, int.MaxValue);

            // If a year is specified, filter transactions to that year.
            if (year.HasValue)
            {
                transactions = transactions.Where(t => t.Date.Year == year.Value).ToList();
            }

            // Filter for ATM transactions that have a 'refund' indicator in their Narrative.
            var atmRefunds = transactions
                .Where(t => t.Type.Equals("ATM", System.StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrEmpty(t.Narrative) &&
                            t.Narrative.ToLower().Contains("refund"))
                .GroupBy(t => t.FromAccount)
                .Select(g => new TopAtmRefundDto
                {
                    AtmIdentifier = g.Key,
                    RefundCount = g.Count()
                })
                .OrderByDescending(x => x.RefundCount)
                .Take(10)
                .ToList();

            return Results.Ok(atmRefunds);
        }

        // New endpoint: Returns the top 10 Reasons with most transactions filtered by year (if provided)
        public static async Task<IResult> GetTopReasons(
             [FromServices] ITransactionRepository transactionRepository,
             [FromQuery] int? year)
        {
            // Retrieve all transactions.
            var transactions = await transactionRepository.GetAllAsync(null, null, null, 1, int.MaxValue);

            // If a year is specified, filter transactions to that year.
            if (year.HasValue)
            {
                transactions = transactions.Where(t => t.Date.Year == year.Value).ToList();
            }

            // Group transactions by the associated Reason (if provided) and count occurrences.
            var topReasons = transactions
                .Where(t => t.Reason != null)
                .GroupBy(t => new { t.Reason.Id, t.Reason.NameAR })
                .Select(g => new TopReasonDto
                {
                    ReasonId = g.Key.Id,
                    ReasonName = g.Key.NameAR,
                    TransactionCount = g.Count()
                })
                .OrderByDescending(r => r.TransactionCount)
                .Take(10)
                .ToList();

            return Results.Ok(topReasons);
        }

        private static async Task<User?> GetCurrentUser(HttpContext context, IUserRepository userRepository)
        {
            var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var authId = int.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var userDetails = await userRepository.GetUserByAuthId(authId, authToken);
            return userDetails != null ? new User { Id = userDetails.UserId } : null;
        }

    }

}