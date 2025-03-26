using BlockingApi.Data.Models;
using BlockingApi.Core.Abstractions;
using BlockingApi.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using BlockingApi.Core.Dtos;
using BlockingApi.Abstractions;

namespace BlockingApi.Endpoints
{
    public class ExternalApiEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var external = app.MapGroup("/api/external").RequireAuthorization("requireAuthUser");

            external.MapPost("/get-customer-info", GetCustomerInfo)
                .RequireAuthorization("ViewCustomers")
                .Produces(200)
                .Produces(400);

            external.MapPost("/customers/block", BlockCustomer)
                .RequireAuthorization("BlockPermission")
                .Produces(200)
                .Produces(400);

            external.MapPost("/customers/unblock", UnblockCustomer)
                .RequireAuthorization("BlockPermission")
                .Produces(200)
                .Produces(400);
        }

        private static async Task<bool> UserHasPermission(ClaimsPrincipal user, string permission, IRoleRepository roleRepository, ILogger logger)
        {
            var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // ✅ Only check `UserRolePermissions` since permissions are already assigned when the user was created.
            var userPermissions = await roleRepository.GetUserPermissions(userId);

            logger.LogInformation("User {UserId} has permissions: {Permissions}", userId, string.Join(", ", userPermissions));

            return userPermissions.Contains(permission);
        }




        public static async Task<IResult> GetCustomerInfo(
    [FromBody] SearchRequestDto request,
    [FromServices] IExternalApiRepository externalApiRepository,
    [FromServices] BlockingApiDbContext context,
    [FromServices] IKycApiRepository kycApiRepository, // New KYC API repository
    ILogger<ExternalApiEndpoints> logger)
        {
            logger.LogInformation("Fetching customer info for {SearchBy}: {SearchTerm}", request.SearchBy, request.SearchTerm);

            // Define the string that indicates where the customer was found
            string foundIn = string.Empty;

            // ✅ Step 1: Handle Search by Fullname
            if (request.SearchBy == "fullname")
            {
                var existingCustomer = await context.Customers
                    .FirstOrDefaultAsync(c => c.FirstName.Contains(request.SearchTerm) || c.NationalId == request.SearchTerm);

                if (existingCustomer != null)
                {
                    foundIn = "Database";
                    logger.LogInformation("Customer found locally by FirstName or NationalId: {CustomerId}", existingCustomer.Id);
                    return Results.Ok(new { Customer = existingCustomer, FoundIn = foundIn });
                }

                // If not found, search in the KYC API
                var kycCustomer = await kycApiRepository.SearchCustomerInKycApi(request.SearchTerm, request.SearchBy, logger, request.KycToken);
                if (kycCustomer != null)
                {
                    foundIn = "KYC API";
                    // Add the customer from KYC API to the local database
                    var branch = await context.Branches.FirstOrDefaultAsync(b => b.CABBN == kycCustomer.BCODE);

                    if (branch == null)
                    {
                        logger.LogError("Branch with BCODE {BranchCode} (CABBN) not found.", kycCustomer.BCODE);
                        return Results.Problem($"Branch with BCODE {kycCustomer.BCODE} not found.");
                    }

                    var newCustomer = new Customer
                    {
                        CID = kycCustomer.CID ?? "No CID provided",
                        FirstName = kycCustomer.CNAME ?? "No name provided",
                        LastName = kycCustomer.LastName ?? string.Empty,  // Ensure LastName is not null
                        Address = kycCustomer.BNAME ?? "No address provided",  // Default value if no address is returned
                        NationalId = kycCustomer.NationalId ?? "No NationalId provided", // Default value if no NationalId is returned
                        BranchId = branch.Id,
                        CreatedAt = DateTime.UtcNow
                    };

                    await context.Customers.AddAsync(newCustomer);
                    await context.SaveChangesAsync();

                    logger.LogInformation("Customer {CustomerId} saved in local database with BranchId {BranchId}.", newCustomer.CID, newCustomer.BranchId);

                    return Results.Ok(new { Customer = newCustomer, FoundIn = foundIn });
                }

                // If no result from KYC API, ask to search by CID
                return Results.BadRequest("Customer not found by the given search criteria. Please search by CID.");
            }

            // ✅ Step 2: Handle Search by NationalId
            if (request.SearchBy == "nationalId")
            {
                var existingCustomer = await context.Customers
                    .FirstOrDefaultAsync(c => c.NationalId == request.SearchTerm);

                if (existingCustomer != null)
                {
                    foundIn = "Database";
                    logger.LogInformation("Customer found locally by NationalId: {CustomerId}", existingCustomer.Id);
                    return Results.Ok(new { Customer = existingCustomer, FoundIn = foundIn });
                }

                // If not found, search in the KYC API
                var kycCustomer = await kycApiRepository.SearchCustomerInKycApi(request.SearchTerm, request.SearchBy, logger, request.KycToken);
                if (kycCustomer != null)
                {
                    foundIn = "KYC API";
                    // Add the customer from KYC API to the local database
                    var branch = await context.Branches.FirstOrDefaultAsync(b => b.CABBN == kycCustomer.BCODE);

                    if (branch == null)
                    {
                        logger.LogError("Branch with BCODE {BranchCode} (CABBN) not found.", kycCustomer.BCODE);
                        return Results.Problem($"Branch with BCODE {kycCustomer.BCODE} not found.");
                    }

                    var newCustomer = new Customer
                    {
                        CID = kycCustomer.CID ?? "No CID provided",
                        FirstName = kycCustomer.CNAME ?? "No name provided",
                        LastName = kycCustomer.LastName ?? string.Empty,  // Ensure LastName is not null
                        Address = kycCustomer.BNAME ?? "No address provided",  // Default value if no address is returned
                        NationalId = kycCustomer.NationalId ?? "No NationalId provided", // Default value if no NationalId is returned
                        BranchId = branch.Id,
                        CreatedAt = DateTime.UtcNow
                    };

                    await context.Customers.AddAsync(newCustomer);
                    await context.SaveChangesAsync();

                    logger.LogInformation("Customer {CustomerId} saved in local database with BranchId {BranchId}.", newCustomer.CID, newCustomer.BranchId);

                    return Results.Ok(new { Customer = newCustomer, FoundIn = foundIn });
                }

                // If no result from KYC API, ask to search by CID
                return Results.BadRequest("Customer not found by the given search criteria. Please search by CID.");
            }

            // ✅ Step 3: Handle Search by CID
            if (request.SearchBy == "cid")
            {
                var existingCustomer = await context.Customers
                    .FirstOrDefaultAsync(c => c.CID == request.SearchTerm);

                if (existingCustomer != null)
                {
                    foundIn = "Database";
                    logger.LogInformation("Customer found locally by CID: {CustomerId}", existingCustomer.Id);
                    return Results.Ok(new { Customer = existingCustomer, FoundIn = foundIn });
                }

                // If not found, search in the KYC API
                var kycCustomer = await kycApiRepository.SearchCustomerInKycApi(request.SearchTerm, request.SearchBy, logger, request.KycToken);
                if (kycCustomer != null)
                {
                    foundIn = "KYC API";
                    // Add the customer from KYC API to the local database
                    var branch = await context.Branches.FirstOrDefaultAsync(b => b.CABBN == kycCustomer.BCODE);

                    if (branch == null)
                    {
                        logger.LogError("Branch with BCODE {BranchCode} (CABBN) not found.", kycCustomer.BCODE);
                        return Results.Problem($"Branch with BCODE {kycCustomer.BCODE} not found.");
                    }

                    var newCustomer = new Customer
                    {
                        CID = kycCustomer.CID ?? "No CID provided",
                        FirstName = kycCustomer.CNAME ?? "No name provided",
                        LastName = kycCustomer.LastName ?? string.Empty,
                        Address = kycCustomer.BNAME ?? "No address provided",
                        NationalId = kycCustomer.NationalId ?? "No NationalId provided",
                        BranchId = branch.Id,
                        CreatedAt = DateTime.UtcNow
                    };

                    await context.Customers.AddAsync(newCustomer);
                    await context.SaveChangesAsync();

                    logger.LogInformation("Customer {CustomerId} saved in local database with BranchId {BranchId}.", newCustomer.CID, newCustomer.BranchId);

                    return Results.Ok(new { Customer = newCustomer, FoundIn = foundIn });
                }

                // If no result from KYC API, search in the external API (7070)
                var externalCustomer = await externalApiRepository.GetCustomerInfo(request.SearchTerm);

                if (externalCustomer != null)
                {
                    foundIn = "External API (7070)";
                    var externalBranch = await context.Branches.FirstOrDefaultAsync(b => b.CABBN == externalCustomer.BCODE);
                    if (externalBranch == null)
                    {
                        logger.LogError("Branch with BCODE {BranchCode} (CABBN) not found.", externalCustomer.BCODE);
                        return Results.Problem($"Branch with BCODE {externalCustomer.BCODE} not found.");
                    }

                    var newExternalCustomer = new Customer
                    {
                        CID = externalCustomer.CID ?? "No CID provided",
                        FirstName = externalCustomer.CNAME ?? "No name provided",
                        Address = externalCustomer.BNAME,
                        BranchId = externalBranch.Id,
                        CreatedAt = DateTime.UtcNow
                    };

                    await context.Customers.AddAsync(newExternalCustomer);
                    await context.SaveChangesAsync();

                    logger.LogInformation("Customer {CustomerId} saved in local database with BranchId {BranchId}.", newExternalCustomer.CID, newExternalCustomer.BranchId);

                    return Results.Ok(new { Customer = newExternalCustomer, FoundIn = foundIn });
                }

                // If not found in all sources, return error
                return Results.BadRequest("Customer not found.");
            }

            // If the searchBy value is invalid
            return Results.BadRequest("Invalid search criteria. Please use 'cid', 'fullname', or 'nationalId'.");
        }

        public static async Task<IResult> BlockCustomer(
             [FromBody] BlockCustomerDto blockDto,
             ClaimsPrincipal user,
             [FromServices] IExternalApiRepository externalApiRepository,
             [FromServices] BlockingApiDbContext context,
             [FromServices] IRoleRepository roleRepository,
             ILogger<ExternalApiEndpoints> logger)
        {
            if (!await UserHasPermission(user, "BlockCustomer", roleRepository, logger))
                return Results.Forbid();

            logger.LogInformation("Blocking customer CID: {CustomerId}", blockDto.CustomerId);

            var customer = await context.Customers
                .Include(c => c.BlockRecords)
                .FirstOrDefaultAsync(c => c.CID == blockDto.CustomerId);

            if (customer == null)
                return Results.NotFound($"Customer {blockDto.CustomerId} not found.");

            var lastBlock = customer.BlockRecords?.OrderByDescending(b => b.BlockDate).FirstOrDefault();
            if (lastBlock != null && lastBlock.ActualUnblockDate == null)
            {
                return Results.BadRequest("Customer is already blocked.");
            }

            // ✅ Get UserId from JWT Token
            var blockedByUserId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // ✅ Call external API to block customer
            var blockSuccess = await externalApiRepository.BlockCustomer(
                blockDto.CustomerId,
                blockDto.ReasonId,
                blockDto.SourceId,
                blockedByUserId,
                blockDto.ToBlockDate, // Optional unblock date
                blockDto.DecisionFromPublicProsecution,
                blockDto.DecisionFromCentralBankGovernor,
                blockDto.DecisionFromFIU,
                blockDto.OtherDecision
            );

            if (!blockSuccess)
            {
                return Results.Problem("Failed to block customer in bank system.");
            }

            return Results.Ok("Customer blocked successfully.");
        }



        public static async Task<IResult> UnblockCustomer(
             [FromBody] UnblockCustomerDto unblockDto,
             ClaimsPrincipal user,
             [FromServices] IExternalApiRepository externalApiRepository,
             [FromServices] BlockingApiDbContext context,
             [FromServices] IRoleRepository roleRepository,
             ILogger<ExternalApiEndpoints> logger)
        {
            if (!await UserHasPermission(user, "UnblockCustomer", roleRepository, logger))
                return Results.Forbid();

            logger.LogInformation("Unblocking customer CID: {CustomerId}", unblockDto.CustomerId);

            var block = await context.BlockRecords
                .Include(b => b.Customer)
                .Where(b => b.Customer.CID == unblockDto.CustomerId && b.ActualUnblockDate == null)
                .OrderByDescending(b => b.BlockDate)
                .FirstOrDefaultAsync();

            if (block == null) return Results.NotFound("No active block found for this customer.");

            var unblockSuccess = await externalApiRepository.UnblockCustomer(unblockDto.CustomerId, unblockDto.UnblockedByUserId);
            if (!unblockSuccess)
            {
                return Results.Problem("Failed to unblock customer in bank system.");
            }

            block.ActualUnblockDate = DateTime.UtcNow;
            block.UnblockedByUserId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            block.Status = "Unblocked";

            await context.SaveChangesAsync();

            return Results.Ok("Customer unblocked successfully.");
        }
    }
}
