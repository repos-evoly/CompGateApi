using System.Security.Claims;
using BlockingApi.Abstractions;
using BlockingApi.Core.Abstractions;
using BlockingApi.Core.Dtos;
using BlockingApi.Data.Abstractions;
using BlockingApi.Data.Models;
using Microsoft.AspNetCore.Mvc;

public class TransactionEndpoints : IEndpoints
{
    public void RegisterEndpoints(WebApplication app)
    {
        var transactions = app.MapGroup("/api/transactions").RequireAuthorization("requireAuthUser");

        // Batch add transactions (initiator provided in the request)
        transactions.MapPost("/batch-add", BatchAddTransactions)
            .Produces(200)
            .Produces(400)
            .RequireAuthorization("ManageTransactions");

        // Get transactions (optional status filter)
        transactions.MapGet("/list", GetTransactions)
            .Produces(200)
            .RequireAuthorization("ManageTransactions");

        // Escalate transaction: accepts from and to user IDs
        transactions.MapPost("/escalate", EscalateTransaction)
            .RequireAuthorization("EscalateTransaction")
            .Produces(200)
            .Produces(400);

        // Return escalated transaction: reverses the escalation
        transactions.MapPost("/return-escalation", ReturnEscalatedTransaction)
            .RequireAuthorization("EscalateEscalation")
            .Produces(200)
            .Produces(400);

        transactions.MapGet("/{transactionId:int}", GetTransactionWithFlow)
              .Produces<TransactionWithFlowDto>(200)
              .Produces(404)
              .RequireAuthorization("ManageTransactions");
    }

    private static async Task<bool> UserHasPermission(ClaimsPrincipal user, string permission, IRoleRepository roleRepository, ILogger logger)
    {
        var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        // ✅ Only check `UserRolePermissions` since permissions are already assigned when the user was created.
        var userPermissions = await roleRepository.GetUserPermissions(userId);

        logger.LogInformation("User {UserId} has permissions: {Permissions}", userId, string.Join(", ", userPermissions));

        return userPermissions.Contains(permission);
    }

    // Batch add transactions endpoint
    public static async Task<IResult> BatchAddTransactions(
        [FromBody] BatchAddTransactionsDto batchDto,
        [FromServices] ITransactionRepository transactionRepository,
        ILogger<TransactionEndpoints> logger)
    {
        if (batchDto.Transactions == null || !batchDto.Transactions.Any())
        {
            return Results.BadRequest("No transactions provided.");
        }

        foreach (var dto in batchDto.Transactions)
        {
            var transaction = new Transaction
            {
                BranchCode = dto.BranchCode,
                Basic = dto.Basic,
                Suffix = dto.Suffix,
                InputBranch = dto.InputBranch,
                DC = dto.DC,
                Amount = dto.Amount,
                CCY = dto.CCY,
                InputBranchNo = dto.InputBranchNo,
                BranchName = dto.BranchName,
                PostingDate = dto.PostingDate,
                Nr1 = dto.Nr1,
                Nr2 = dto.Nr2,
                Timestamp = dto.Timestamp,
                Status = string.IsNullOrEmpty(dto.Status) ? "Pending" : dto.Status,
                Initiator = dto.Initiator,
                CurrentParty = dto.CurrentParty ?? ""
            };

            await transactionRepository.AddTransactionAsync(transaction);
        }

        return Results.Ok("Transactions added successfully.");
    }

    // Get transactions endpoint with optional status filter.
    public static async Task<IResult> GetTransactions(
        [FromQuery] string? status,
        [FromServices] ITransactionRepository transactionRepository)
    {
        var transactions = await transactionRepository.GetAllTransactionsAsync();
        if (!string.IsNullOrEmpty(status))
        {
            transactions = transactions
                .Where(t => t.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        return Results.Ok(transactions);
    }

    // Escalate a transaction: from a user to a user.
    public static async Task<IResult> EscalateTransaction(
    [FromBody] EscalateTransactionDto escalateDto,
    [FromServices] ITransactionRepository transactionRepository,
    [FromServices] INotificationRepository notificationRepository,
    ILogger<TransactionEndpoints> logger)
    {
        // Retrieve the transaction.
        var transaction = await transactionRepository.GetTransactionByIdAsync(escalateDto.TransactionId);
        if (transaction == null)
        {
            logger.LogWarning("Transaction with Id {TransactionId} not found", escalateDto.TransactionId);
            return Results.NotFound("Transaction not found");
        }

        // Update the transaction: mark as escalated.
        transaction.Status = "Escalated";
        transaction.Initiator = escalateDto.FromUserId.ToString();
        transaction.CurrentParty = escalateDto.ToUserId.ToString();
        await transactionRepository.UpdateTransactionAsync(transaction);

        // Create a new notification for the escalated user
        var notification = new Notification
        {
            FromUserId = escalateDto.FromUserId,
            ToUserId = escalateDto.ToUserId,
            Subject = "Transaction Escalated",
            Message = $"Transaction {transaction.Id} has been escalated to you for further action.",
            Link = $"transactions/{transaction.Id}?100",  // Link to transaction details in frontend
        };

        await notificationRepository.AddNotificationAsync(notification);

        // Log the escalation in the transaction flow.
        var transactionFlow = new TransactionFlow
        {
            TransactionId = transaction.Id,
            FromUserId = escalateDto.FromUserId,
            ToUserId = escalateDto.ToUserId,
            Action = "Escalated",
            ActionDate = DateTime.UtcNow,
            Remark = $"Escalated from user {escalateDto.FromUserId} to user {escalateDto.ToUserId}",
            CanReturn = true
        };

        await transactionRepository.AddTransactionFlowAsync(transactionFlow);

        return Results.Ok("Transaction escalated successfully");
    }

    public static async Task<IResult> ReturnEscalatedTransaction(
     [FromBody] ReturnEscalationDto returnDto,
     [FromServices] ITransactionRepository transactionRepository,
     [FromServices] INotificationRepository notificationRepository,
     ILogger<TransactionEndpoints> logger)
    {
        // Retrieve the transaction.
        var transaction = await transactionRepository.GetTransactionByIdAsync(returnDto.TransactionId);
        if (transaction == null)
        {
            logger.LogWarning("Transaction with Id {TransactionId} not found", returnDto.TransactionId);
            return Results.NotFound("Transaction not found");
        }

        // Update the transaction: mark as "Returned".
        transaction.Status = "Returned";
        transaction.CurrentParty = returnDto.ToUserId.ToString();
        await transactionRepository.UpdateTransactionAsync(transaction);

        // Create a new notification for the user who is returning the escalation
        var notification = new Notification
        {
            FromUserId = returnDto.FromUserId,
            ToUserId = returnDto.ToUserId,
            Subject = "Transaction Return",
            Message = $"Transaction {transaction.Id} has been returned to you for review.",
            Link = $"transactions/{transaction.Id}?100",  // Link to transaction details in frontend
        };

        await notificationRepository.AddNotificationAsync(notification);

        // Log the return in the transaction flow.
        var transactionFlow = new TransactionFlow
        {
            TransactionId = transaction.Id,
            FromUserId = returnDto.FromUserId,
            ToUserId = returnDto.ToUserId,
            Action = "Return",
            ActionDate = DateTime.UtcNow,
            Remark = $"Transaction returned from user {returnDto.FromUserId} to user {returnDto.ToUserId}",
            CanReturn = false
        };

        await transactionRepository.AddTransactionFlowAsync(transactionFlow);

        return Results.Ok("Transaction returned successfully");
    }

    public static async Task<IResult> GetTransactionWithFlow(
    int transactionId,
    [FromServices] ITransactionRepository transactionRepository,
    [FromServices] IUserRepository userRepository,
    [FromServices] IBranchRepository branchRepository,
    ILogger<TransactionEndpoints> logger,
    [FromHeader(Name = "Authorization")] string authToken) // Get the token from headers
    {
        // Retrieve the transaction by transactionId
        var transaction = await transactionRepository.GetTransactionByIdAsync(transactionId);
        if (transaction == null)
        {
            return Results.NotFound($"Transaction with Id {transactionId} not found.");
        }

        // Retrieve the associated transaction flow
        var transactionFlows = await transactionRepository.GetTransactionFlowsByTransactionIdAsync(transactionId);

        // Fetch user and branch details for the transaction and its flow
        var fromUser = await userRepository.GetUserById(transaction.ApprovedByUserId ?? 0, authToken);
        var toUser = await userRepository.GetUserById(transaction.CurrentParty != null ? int.Parse(transaction.CurrentParty) : 0, authToken);
        var branch = await branchRepository.GetBranchById(transaction.BranchCode);

        // Prepare the response DTO
        var transactionWithFlowDto = new TransactionWithFlowDto
        {
            Transaction = new TransactionDto
            {
                Id = transaction.Id,
                BranchCode = transaction.BranchCode,
                BranchName = branch?.Name ?? "Unknown",
                Basic = transaction.Basic,
                Suffix = transaction.Suffix,
                InputBranch = transaction.InputBranch,
                DC = transaction.DC,
                Amount = transaction.Amount,
                CCY = transaction.CCY,
                InputBranchNo = transaction.InputBranchNo,
                PostingDate = transaction.PostingDate,
                Nr1 = transaction.Nr1,
                Nr2 = transaction.Nr2,
                Timestamp = transaction.Timestamp,
                Status = transaction.Status,
                Owner = fromUser?.FirstName ?? "Unknown",
                CurrentParty = toUser?.FirstName ?? "Unknown"
            },
            TransactionFlows = transactionFlows.Select(tf => new TransactionFlowDto
            {
                Id = tf.Id,
                Action = tf.Action,
                ActionDate = tf.ActionDate,
                FromUserId = tf.FromUserId,
                ToUserId = tf.ToUserId ?? 0,
                Remark = tf.Remark,
                CanReturn = tf.CanReturn,
                FromUserName = tf.FromUser?.FirstName ?? "Unknown",
                ToUserName = tf.ToUser?.FirstName ?? "Unknown"
            }).ToList()
        };

        return Results.Ok(transactionWithFlowDto);
    }


}
