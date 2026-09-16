using System.Security.Claims;
using CompGateApi.Abstractions;
using CompGateApi.Core.Authentication;
using CompGateApi.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Endpoints;

/// <summary>
/// Narrow service-authenticated user lookups used by the Mobile BFF.
/// These endpoints deliberately avoid CompAuthApi, BankApi, and other downstream calls.
/// </summary>
public sealed class MobileInternalUserEndpoints : IEndpoints
{
    public void RegisterEndpoints(WebApplication app)
    {
        var users = app.MapGroup("/api/mobile-internal/users")
            .RequireAuthorization(
                MobileServiceAuthenticationDefaults.RequireCompanyUserAndMobileBffServicePolicy)
            .WithTags("Mobile Internal Users");

        users.MapGet("/{authUserId:int}/access-context", GetAccessContext)
            .WithName("GetMobileUserAccessContext")
            .Produces<MobileUserAccessContextResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetAccessContext(
        int authUserId,
        ClaimsPrincipal principal,
        CompGateApiDbContext db,
        ILogger<MobileInternalUserEndpoints> logger,
        CancellationToken cancellationToken)
    {
        if (authUserId <= 0 || !TryGetAuthUserId(principal, out var callerAuthUserId))
        {
            return Results.Unauthorized();
        }

        var target = await db.Users
            .AsNoTracking()
            .Where(user => user.AuthUserId == authUserId)
            .Select(user => new
            {
                user.Id,
                user.AuthUserId,
                user.CompanyId,
                CompanyCode = user.Company != null ? user.Company.Code : null,
                user.IsActive,
                user.IsCompanyAdmin
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (target is null)
        {
            return Results.NotFound();
        }

        // A user may read their own context. Looking up another user is limited to
        // an active company administrator in the same company (device enrollment).
        if (callerAuthUserId != target.AuthUserId)
        {
            var caller = await db.Users
                .AsNoTracking()
                .Where(user => user.AuthUserId == callerAuthUserId)
                .Select(user => new
                {
                    user.CompanyId,
                    user.IsActive,
                    user.IsCompanyAdmin
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (caller is null ||
                !caller.IsActive ||
                !caller.IsCompanyAdmin ||
                caller.CompanyId is null ||
                caller.CompanyId != target.CompanyId)
            {
                logger.LogWarning(
                    "Mobile BFF user-context lookup was denied for caller {CallerAuthUserId} and target {TargetAuthUserId}.",
                    callerAuthUserId,
                    authUserId);
                return Results.Forbid();
            }
        }

        var permissions = await (
                from assignment in db.UserRolePermissions.AsNoTracking()
                join permission in db.Permissions.AsNoTracking()
                    on assignment.PermissionId equals permission.Id
                where assignment.UserId == target.Id
                select permission.NameEn)
            .Distinct()
            .ToListAsync(cancellationToken);

        return Results.Ok(new MobileUserAccessContextResponse(
            target.AuthUserId,
            target.CompanyCode,
            target.IsActive,
            target.IsCompanyAdmin,
            permissions));
    }

    private static bool TryGetAuthUserId(
        ClaimsPrincipal principal,
        out int authUserId)
    {
        var value = principal.Claims
            .FirstOrDefault(claim =>
                claim.Type == "nameid" || claim.Type == ClaimTypes.NameIdentifier)
            ?.Value;
        return int.TryParse(value, out authUserId) && authUserId > 0;
    }
}

public sealed record MobileUserAccessContextResponse(
    int AuthUserId,
    string? CompanyCode,
    bool IsActive,
    bool IsCompanyAdmin,
    IReadOnlyList<string> Permissions);
