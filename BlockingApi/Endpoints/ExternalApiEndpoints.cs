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
            // Extract AuthUserId from the JWT token
            var authUserId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Fetch the user using AuthUserId to get UserId
            var userEntity = await roleRepository.GetUserByAuthUserId(authUserId); // Ensure this method returns a user entity
            if (userEntity == null)
            {
                logger.LogWarning("User with AuthUserId {AuthUserId} not found.", authUserId);
                return false;
            }

            // Now we have the UserId from the userEntity
            var userId = userEntity.Id;

            // Retrieve the user's permissions
            var userPermissions = await roleRepository.GetUserPermissions(userId); // Assuming this method returns a list of permissions for the user

            logger.LogInformation("User {UserId} has permissions: {Permissions}", userId, string.Join(", ", userPermissions));

            return userPermissions.Contains(permission);
        }





        public static async Task<IResult> GetCustomerInfo(
        [FromBody] SearchRequestDto request,
        [FromServices] IExternalApiRepository externalApiRepository,
        [FromServices] BlockingApiDbContext context,
        [FromServices] IKycApiRepository kycApiRepository, // New KYC API repository
        [FromServices] IRoleRepository roleRepository,
        ClaimsPrincipal user, // Injected ClaimsPrincipal
        ILogger<ExternalApiEndpoints> logger)
        {
            if (!await UserHasPermission(user, "ViewCustomers", roleRepository, logger))
                return Results.Forbid();
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
                        CreatedAt = DateTimeOffset.Now
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
                        LastName = kycCustomer.LastName ?? string.Empty,
                        Address = kycCustomer.BNAME ?? "No address provided",
                        NationalId = kycCustomer.NationalId ?? "No NationalId provided",
                        BranchId = branch.Id,
                        CreatedAt = DateTimeOffset.Now
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
                        CreatedAt = DateTimeOffset.Now
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
                        CreatedAt = DateTimeOffset.Now
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
    [FromServices] BlockingApiDbContext context,
    [FromServices] IExternalApiRepository externalApiRepository,
    [FromServices] IRoleRepository roleRepository,
    ILogger<ExternalApiEndpoints> logger)
        {
            if (!await UserHasPermission(user, "BlockPermission", roleRepository, logger))
                return Results.Forbid();
            // Validate BlockedByUserId to ensure it exists in the database
            var blockedByUser = await context.Users.FirstOrDefaultAsync(u => u.Id == blockDto.BlockedByUserId);
            if (blockedByUser == null)
            {
                logger.LogError("User with ID {BlockedByUserId} not found.", blockDto.BlockedByUserId);
                return Results.BadRequest("Invalid user for blocking operation.");
            }

            // Log information about the user performing the block
            logger.LogInformation("Received BlockCustomer request from user {BlockedByUserId}", blockDto.BlockedByUserId);

            // Fetch the customer to block
            var customer = await context.Customers
                .Include(c => c.BlockRecords)
                .FirstOrDefaultAsync(c => c.CID == blockDto.CustomerId);

            if (customer == null)
            {
                logger.LogError("Customer {CustomerId} not found.", blockDto.CustomerId);
                return Results.NotFound($"Customer {blockDto.CustomerId} not found.");
            }

            // Check if the customer is already blocked
            var lastBlock = customer.BlockRecords?.OrderByDescending(b => b.BlockDate).FirstOrDefault();
            if (lastBlock != null && lastBlock.ActualUnblockDate == null)
            {
                logger.LogWarning("Customer {CustomerId} is already blocked.", blockDto.CustomerId);
                return Results.BadRequest("Customer is already blocked.");
            }

            // Log the block attempt
            logger.LogInformation("Attempting to block customer {CustomerId} by user {BlockedByUserId}", blockDto.CustomerId, blockDto.BlockedByUserId);

            // Call external API to block the customer
            var blockSuccess = await externalApiRepository.BlockCustomer(
                blockDto.CustomerId,
                blockDto.ReasonId,
                blockDto.SourceId,
                blockDto.BlockedByUserId, // Use the BlockedByUserId directly from the DTO
                blockDto.ToBlockDate, // Optional unblock date
                blockDto.DecisionFromPublicProsecution,
                blockDto.DecisionFromCentralBankGovernor,
                blockDto.DecisionFromFIU,
                blockDto.OtherDecision
            );

            if (!blockSuccess)
            {
                logger.LogError("Failed to block customer {CustomerId} in external system.", blockDto.CustomerId);
                return Results.Problem("Failed to block customer in bank system.");
            }

            // Log success
            logger.LogInformation("Customer {CustomerId} blocked successfully by user {BlockedByUserId}.", blockDto.CustomerId, blockDto.BlockedByUserId);

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
            if (!await UserHasPermission(user, "UnblockPermission", roleRepository, logger))
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

            block.ActualUnblockDate = DateTimeOffset.Now;
            block.UnblockedByUserId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            block.Status = "Unblocked";

            await context.SaveChangesAsync();

            return Results.Ok("Customer unblocked successfully.");
        }
    }
}
