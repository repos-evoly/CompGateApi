using System.Security.Claims;
using BlockingApi.Abstractions;
using BlockingApi.Core.Abstractions;
using BlockingApi.Core.Dtos;
using BlockingApi.Data.Abstractions;
using BlockingApi.Data.Models;
using BlockingApi.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

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
            .RequireAuthorization("EscalateTransactions")
            .Produces(200)
            .Produces(400);

        // Return escalated transaction: reverses the escalation
        transactions.MapPost("/return-escalation", ReturnEscalatedTransaction)
            .RequireAuthorization("EscalateTransactions")
            .Produces(200)
            .Produces(400);

        transactions.MapGet("/{transactionId:int}", GetTransactionWithFlow)
              .Produces<TransactionWithFlowDto>(200)
              .Produces(404)
              .RequireAuthorization("ManageTransactions");

        transactions.MapPost("/edit-party", EditTransactionParty)
                .RequireAuthorization("ManageTransactions")
                .Produces(200)
                .Produces(400)
                .Produces(404);

        transactions.MapPost("/delete", DeleteTransaction)
                .RequireAuthorization("ManageTransactions")
                .Produces(200)
                .Produces(400)
                .Produces(404);

        transactions.MapPost("/approve-deny", ApproveOrDenyTransaction)
                .RequireAuthorization("ManageTransactions")
                .Produces(200)
                .Produces(400)
                .Produces(404);
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
        [FromServices] IUserRepository userRepository,
        [FromServices] IBranchRepository branchRepository,
         [FromServices] INotificationRepository notificationRepository,
        [FromServices] IHubContext<NotificationHub> hubContext,
        HttpContext context,
        ILogger<TransactionEndpoints> logger)
    {
        if (batchDto.Transactions == null || !batchDto.Transactions.Any())
        {
            return Results.BadRequest("No transactions provided.");
        }

        var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        var currentUserAuthId = int.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var currentUser = await userRepository.GetUserByAuthId(currentUserAuthId, authToken);

        if (currentUser == null)
            return Results.BadRequest("Invalid user");

        foreach (var dto in batchDto.Transactions)
        {
            var initiatorId = currentUser.UserId;
            int? currentPartyId = null;
            bool createEscalationFlow = false;

            var isEscalated = dto.Escalate && dto.CurrentPartyUserId.HasValue;

            if (isEscalated)
            {
                // Maker or Manager can escalate explicitly
                initiatorId = dto.InitiatorUserId ?? currentUser.UserId;
                currentPartyId = dto.CurrentPartyUserId;
                createEscalationFlow = true;
            }
            else
            {
                // If not escalated, determine role logic
                var roleName = currentUser.Role?.NameLT.ToLower() ?? string.Empty;

                if (roleName.Contains("auditor") || roleName.Contains("checker") || roleName.Contains("viewer"))
                {
                    initiatorId = currentUser.UserId;
                    // Retrieve branch details for logging.
                    var branch = await branchRepository.GetBranchById(currentUser.BranchId.ToString());
                    if (branch == null)
                    {
                        logger.LogWarning("Branch not found for BranchId: {BranchId}", currentUser.BranchId);
                    }
                    else
                    {
                        logger.LogInformation("Retrieved branch details: {@Branch}", branch);
                        if (branch.Area != null)
                        {
                            logger.LogInformation("Branch Area: {@Area}", branch.Area);
                            logger.LogInformation("Area HeadOfSectionId: {HeadOfSectionId}", branch.Area.HeadOfSectionId);
                        }
                        else
                        {
                            logger.LogWarning("No Area found for branch {BranchId}", branch.CABBN);
                        }
                    }
                    // Assign currentPartyId from branch's Area.HeadOfSectionId (could be null).
                    currentPartyId = branch?.Area?.HeadOfSectionId;
                }
                else if (roleName.Contains("maker") || roleName.Contains("manager") || roleName.Contains("admin") || roleName.Contains("assistantmanager") || roleName.Contains("deputymanager"))
                {
                    // Maker/Manager can assign to self for pending
                    initiatorId = currentUser.UserId;
                    currentPartyId = currentUser.UserId;
                }
            }

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
                InitiatorUserId = initiatorId,
                CurrentPartyUserId = currentPartyId,
                TrxTagCode = dto.TrxTagCode ?? string.Empty,
                TrxTag = dto.TrxTag ?? string.Empty,
                TrxSeq = dto.TrxSeq ?? 0,
                ReconRef = dto.ReconRef,
                EventKey = dto.EventKey
            };

            await transactionRepository.AddTransactionAsync(transaction);

            if (createEscalationFlow)
            {
                var flow = new TransactionFlow
                {
                    TransactionId = transaction.Id,
                    FromUserId = initiatorId,
                    ToUserId = currentPartyId,
                    Action = "Escalated",
                    ActionDate = DateTimeOffset.Now,
                    Remark = dto.Remark ?? string.Empty,
                    CanReturn = true
                };
                await transactionRepository.AddTransactionFlowAsync(flow);
                // Send notification for the escalated transaction
                var notification = new Notification
                {
                    FromUserId = initiatorId,
                    ToUserId = currentPartyId ?? throw new InvalidOperationException("currentPartyId cannot be null when escalation is true"), // Ensure currentPartyId is not null when escalation is true
                    Subject = "تم احالة المعاملة",
                    Message = $"لقد تم إحالة المعاملة رقم {transaction.Id} إليك لاتخاذ الإجراءات الإضافية.",
                    Link = $"transactions/{transaction.Id}"
                }
                ;

                await notificationRepository.AddNotificationAsync(notification);

                await hubContext.Clients.All.SendAsync("ReceiveNotification", new
                {
                    notification.Id,
                    notification.Subject,
                    notification.Message,
                    notification.CreatedAt,
                    UserId = notification.ToUserId
                });
            }
        }

        return Results.Ok("Transactions added successfully.");
    }

    // Get transactions endpoint with optional status filter.
    public static async Task<IResult> GetTransactions(
       [FromQuery] string? status,
       [FromServices] ITransactionRepository transactionRepository,
       HttpContext context)
    {
        int userId = AuthUserId(context);

        // Retrieve transactions relevant to the current user.
        var transactions = await transactionRepository.GetUserTransactionsAsync(userId);

        // Optionally filter by status if provided.
        if (!string.IsNullOrEmpty(status))
        {
            transactions = transactions
                .Where(t => t.Status.Equals(status, System.StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Project each transaction into TransactionDto with full names.
        var result = transactions.Select(t => new TransactionDto
        {
            Id = t.Id,
            BranchCode = t.BranchCode,
            BranchName = t.BranchName,
            Basic = t.Basic,
            Suffix = t.Suffix,
            InputBranch = t.InputBranch,
            DC = t.DC,
            Amount = t.Amount,
            CCY = t.CCY,
            InputBranchNo = t.InputBranchNo,
            PostingDate = t.PostingDate,
            Nr1 = t.Nr1,
            Nr2 = t.Nr2,
            Timestamp = t.Timestamp,
            Status = t.Status,
            InitiatorUserId = t.InitiatorUserId,
            CurrentPartyUserId = t.CurrentPartyUserId,
            EventKey = t.EventKey,
            TrxTagCode = t.TrxTagCode,
            TrxTag = t.TrxTag,
            TrxSeq = t.TrxSeq,
            ReconRef = t.ReconRef,

            InitiatorName = t.InitiatorUser != null
                              ? $"{t.InitiatorUser.FirstName} {t.InitiatorUser.LastName}"
                              : string.Empty,
            CurrentPartyName = t.CurrentPartyUser != null
                              ? $"{t.CurrentPartyUser.FirstName} {t.CurrentPartyUser.LastName}"
                              : string.Empty
        }).ToList();

        return Results.Ok(result);
    }



    // Escalate a transaction: from a user to a user.
    public static async Task<IResult> EscalateTransaction(
       [FromBody] EscalateTransactionDto escalateDto,
       [FromServices] ITransactionRepository transactionRepository,
       [FromServices] INotificationRepository notificationRepository,
       [FromServices] IHubContext<NotificationHub> hubContext,
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
        transaction.InitiatorUserId = escalateDto.FromUserId;
        transaction.CurrentPartyUserId = escalateDto.ToUserId;
        await transactionRepository.UpdateTransactionAsync(transaction);

        // Create a new notification for the escalated user
        var notification = new Notification
        {
            FromUserId = escalateDto.FromUserId,
            ToUserId = escalateDto.ToUserId,
            Subject = "تم احالة المعاملة",
            Message = $"لقد تم إحالة المعاملة رقم {transaction.Id} إليك لاتخاذ الإجراءات الإضافية.",
            Link = $"transactions/{transaction.Id}"
        };

        await notificationRepository.AddNotificationAsync(notification);

        await hubContext.Clients.All.SendAsync("ReceiveNotification", new
        {
            NotificationId = notification.Id,
            NotificationSubject = notification.Subject,
            NotificationMessage = notification.Message,
            Created = notification.CreatedAt,
            UserId = notification.ToUserId  // Added user id field
        });

        // Log the escalation in the transaction flow.
        var transactionFlow = new TransactionFlow
        {
            TransactionId = transaction.Id,
            FromUserId = escalateDto.FromUserId,
            ToUserId = escalateDto.ToUserId,
            Action = "Escalated",
            ActionDate = DateTimeOffset.Now,
            Remark = escalateDto.Remark,  // Store the remark
            CanReturn = true
        };

        await transactionRepository.AddTransactionFlowAsync(transactionFlow);

        return Results.Ok("Transaction escalated successfully");
    }

    public static async Task<IResult> ReturnEscalatedTransaction(
        [FromBody] ReturnEscalationDto returnDto,
        [FromServices] ITransactionRepository transactionRepository,
        [FromServices] ITransactionFlowRepository transactionFlowRepository,
        [FromServices] INotificationRepository notificationRepository,
        [FromServices] IHubContext<NotificationHub> hubContext,
        ILogger<TransactionEndpoints> logger)
    {
        // Retrieve the transaction.
        var transaction = await transactionRepository.GetTransactionByIdAsync(returnDto.TransactionId);
        if (transaction == null)
        {
            logger.LogWarning("Transaction with Id {TransactionId} not found", returnDto.TransactionId);
            return Results.NotFound("Transaction not found");
        }

        // Mark the current party as the user to return the escalation
        transaction.CurrentPartyUserId = returnDto.ToUserId;
        await transactionRepository.UpdateTransactionAsync(transaction);

        // Create a new notification for the user who is returning the escalation
        var notification = new Notification
        {
            FromUserId = returnDto.FromUserId,
            ToUserId = returnDto.ToUserId,
            Subject = "Returned",
            Message = $"Transaction {transaction.Id} has been returned to you for review.",
            Link = $"transactions/{transaction.Id}",  // Link to transaction details in frontend
        };

        await notificationRepository.AddNotificationAsync(notification);

        await hubContext.Clients.All.SendAsync("ReceiveNotification", new
        {
            NotificationId = notification.Id,
            NotificationSubject = notification.Subject,
            NotificationMessage = notification.Message,
            Created = notification.CreatedAt,
            UserId = notification.ToUserId  // Added user id field
        });



        // Log the return in the transaction flow.
        var transactionFlow = new TransactionFlow
        {
            TransactionId = transaction.Id,
            FromUserId = returnDto.FromUserId,
            ToUserId = returnDto.ToUserId,
            Action = "Return",
            ActionDate = DateTimeOffset.Now,
            Remark = returnDto.Remark,  // Store the remark
            CanReturn = false // Mark that the transaction can't be returned anymore
        };

        await transactionFlowRepository.UpdateTransactionFlowAsync(transactionFlow);  // Correct repository call

        // Update the first escalation flow's CanReturn to false
        var firstEscalationFlow = await transactionFlowRepository.GetTransactionFlowByTransactionIdAsync(transaction.Id);
        var escalationFlow = firstEscalationFlow?.FirstOrDefault(tf => tf.Action == "Escalated");

        if (escalationFlow != null)
        {
            escalationFlow.CanReturn = false;
            await transactionFlowRepository.UpdateTransactionFlowAsync(escalationFlow);  // Update the escalation flow
        }

        return Results.Ok("Transaction returned successfully");
    }

    public static async Task<IResult> GetTransactionWithFlow(
        int transactionId,
        HttpContext context,
        [FromServices] ITransactionRepository transactionRepository,
        [FromServices] IUserRepository userRepository,
        [FromServices] IBranchRepository branchRepository,
        ILogger<TransactionEndpoints> logger)
    {
        // Extract the Authorization token from the request header
        var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        // Log the information for debugging
        logger.LogInformation("Fetching transaction with ID {TransactionId} using token {AuthToken}", transactionId, authToken);

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
        var toUser = await userRepository.GetUserById(transaction.CurrentPartyUserId.HasValue ? transaction.CurrentPartyUserId.Value : 0, authToken);
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
                InitiatorUserId = transaction.InitiatorUserId,
                CurrentPartyUserId = transaction.CurrentPartyUserId,
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

    public static async Task<IResult> EditTransactionParty(
        [FromBody] TransactionEditDto editDto,
        [FromServices] ITransactionRepository transactionRepository,
        [FromServices] ITransactionFlowRepository transactionFlowRepository,
        [FromServices] IUserRepository userRepository,
        HttpContext context,
        ILogger<TransactionEndpoints> logger)
    {
        var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        // Retrieve the transaction by ID
        var transaction = await transactionRepository.GetTransactionByIdAsync(editDto.TransactionId);
        if (transaction == null)
        {
            return Results.NotFound("Transaction not found.");
        }

        // Get the current user using the auth token and name identifier
        var currentUserAuthId = int.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var currentUser = await userRepository.GetUserByAuthId(currentUserAuthId, authToken);
        if (currentUser == null)
        {
            return Results.BadRequest("Invalid user");
        }

        if (transaction.InitiatorUserId != currentUser.UserId)
        {
            return Results.Json(new { error = "You are not authorized to modify this transaction." }, statusCode: 401);
        }

        // Update the currentParty in the transaction
        transaction.CurrentPartyUserId = editDto.ToUserId;
        await transactionRepository.UpdateTransactionAsync(transaction);

        // Retrieve the associated transaction flow (get the first flow, assuming only one flow exists)
        var transactionFlow = await transactionFlowRepository.GetTransactionFlowByTransactionIdAsync(editDto.TransactionId);
        var singleFlow = transactionFlow.FirstOrDefault(); // Get the first flow from the collection

        if (singleFlow == null)
        {
            return Results.NotFound("Transaction flow not found.");
        }

        // Update the ToUserId in the transaction flow
        singleFlow.ToUserId = editDto.ToUserId;
        await transactionFlowRepository.UpdateTransactionFlowAsync(singleFlow);

        // Log the update
        logger.LogInformation($"Transaction {editDto.TransactionId} currentParty and ToUserId updated successfully.");

        return Results.Ok("Transaction updated successfully.");
    }

    public static async Task<IResult> DeleteTransaction(
        [FromBody] DeleteTransactionDto deleteDto,
        [FromServices] ITransactionRepository transactionRepository,
        [FromServices] ITransactionFlowRepository transactionFlowRepository,
        [FromServices] IUserRepository userRepository,
        HttpContext context,
        ILogger<TransactionEndpoints> logger)
    {
        var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        // Retrieve the transaction by ID
        var transaction = await transactionRepository.GetTransactionByIdAsync(deleteDto.TransactionId);
        if (transaction == null)
        {
            return Results.NotFound($"Transaction with Id {deleteDto.TransactionId} not found.");
        }

        // Get the current user using the auth token and name identifier
        var currentUserAuthId = int.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var currentUser = await userRepository.GetUserByAuthId(currentUserAuthId, authToken);
        if (currentUser == null)
        {
            return Results.BadRequest("Invalid user");
        }

        if (transaction.InitiatorUserId != currentUser.UserId)
        {
            return Results.Unauthorized();
        }

        // Retrieve the associated transaction flows (ensure it's a collection)
        var transactionFlows = await transactionFlowRepository.GetTransactionFlowByTransactionIdAsync(deleteDto.TransactionId);

        // Check if the transaction flow exists, has exactly one flow, and its status is "Escalated"
        if (transactionFlows == null || transactionFlows.Count() != 1 || transactionFlows.First().Action != "Escalated")
        {
            return Results.BadRequest("Transaction cannot be deleted. It either has more than one flow or is not in 'Escalated' status.");
        }

        // Delete the transaction (cascade delete will remove associated flows)
        await transactionRepository.DeleteTransactionAsync(deleteDto.TransactionId);

        // Log the deletion
        logger.LogInformation($"Transaction with Id {deleteDto.TransactionId} and its flow deleted successfully.");

        return Results.Ok("Transaction and its flow deleted successfully.");
    }

    public static async Task<IResult> ApproveOrDenyTransaction(
        [FromBody] ApproveOrDenyTransactionDto approveOrDenyDto,
        [FromServices] ITransactionRepository transactionRepository,
        [FromServices] ITransactionFlowRepository transactionFlowRepository,
        [FromServices] IUserRepository userRepository,
        HttpContext context,
        ILogger<TransactionEndpoints> logger)
    {
        var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        // Retrieve the transaction by ID
        var transaction = await transactionRepository.GetTransactionByIdAsync(approveOrDenyDto.TransactionId);
        if (transaction == null)
        {
            return Results.NotFound($"Transaction with Id {approveOrDenyDto.TransactionId} not found.");
        }

        // Get the current user using the auth token and name identifier
        var currentUserAuthId = int.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var currentUser = await userRepository.GetUserByAuthId(currentUserAuthId, authToken);
        if (currentUser == null)
        {
            return Results.BadRequest("Invalid user");
        }

        if (transaction.InitiatorUserId != currentUser.UserId)
        {
            return Results.Json(new { error = "You are not authorized to approve/deny this transaction." }, statusCode: 401);
        }

        // Update the transaction status to either "Approved" or "Reejected"
        transaction.Status = approveOrDenyDto.Action == "Approved" ? "Approved" : "Rejected";

        // Set the ApprovedByUserId to the current user (the one who is approving/denying)
        transaction.ApprovedByUserId = currentUser.UserId;

        await transactionRepository.UpdateTransactionAsync(transaction);

        // Retrieve the associated transaction flow (get the first flow, assuming only one flow exists)
        // Create a new transaction flow for marking the transaction as done
        var newTransactionFlow = new TransactionFlow
        {
            TransactionId = transaction.Id,
            FromUserId = currentUser.UserId,
            ToUserId = currentUser.UserId, // Adjust if necessary for your business logic
            Action = "Done",
            ActionDate = DateTimeOffset.Now,
            Remark = "Transaction approved/rejected and marked as done."
        };

        await transactionRepository.AddTransactionFlowAsync(newTransactionFlow);


        // Log the update
        logger.LogInformation($"Transaction {approveOrDenyDto.TransactionId} status set to {transaction.Status}, flow marked as 'Done', and ApprovedByUserId set to {currentUser.UserId}.");

        return Results.Ok("Transaction successfully approved/rejected and flow marked as 'Done'.");
    }
    private static int AuthUserId(HttpContext context)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out int userId) ? userId : 0;
    }
}
