using System.Net;
using System.Security.Claims;
using CompGateApi.Abstractions;
using CompGateApi.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Endpoints;

public sealed class BankCompanyUsersEndpoints : IEndpoints
{
    public void RegisterEndpoints(WebApplication app)
    {
        var users = app.MapGroup("/api/admin/company-users")
            .RequireAuthorization("RequireCompanyUser").RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));
        users.MapGet("/", ListAsync);
        users.MapGet("/access", CheckAccessAsync);
    }

    private static async Task<IResult> CheckAccessAsync(
        HttpContext context, CompGateApiDbContext db, [FromQuery] int[] targetAuthUserId)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!await IsBankStaffAsync(context, db)) return Results.Forbid();
        if (targetAuthUserId.Length > 50 || targetAuthUserId.Any(id => id <= 0)) return Results.BadRequest();
        if (targetAuthUserId.Length > 0)
        {
            var count = await db.Users.AsNoTracking()
                .Where(user => user.CompanyId != null && targetAuthUserId.Contains(user.AuthUserId))
                .Select(user => user.AuthUserId).Distinct().CountAsync(context.RequestAborted);
            if (count != targetAuthUserId.Distinct().Count()) return Results.Forbid();
        }
        return Results.NoContent();
    }

    private static async Task<IResult> ListAsync(HttpContext context, CompGateApiDbContext db,
        IHttpClientFactory clients, int page = 1, string? searchTerm = null, string? searchBy = null)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!await IsBankStaffAsync(context, db)) return Results.Forbid();
        searchTerm = searchTerm?.Trim();
        if (searchTerm is { Length: > 150 } || page is < 1 or > 100_000) return Results.BadRequest();

        var query = db.Users.AsNoTracking().Where(user => user.CompanyId != null && user.AuthUserId > 0);
        if (!string.IsNullOrEmpty(searchTerm))
        {
            if (searchBy == "username")
            {
                var result = await FindAuthUserIdAsync(context, clients, searchTerm);
                if (result.Error is not null) return result.Error;
                query = query.Where(user => user.AuthUserId == result.UserId);
            }
            else if (searchBy == "companyCode")
                query = query.Where(user => user.Company!.Code.Contains(searchTerm));
            else if (searchBy == "email")
                query = query.Where(user => user.Email.Contains(searchTerm));
            else
                query = query.Where(user => (user.FirstName + " " + user.LastName).Contains(searchTerm));
        }

        const int pageSize = 25;
        var count = await query.CountAsync(context.RequestAborted);
        var data = await query.OrderBy(user => user.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(user => new
            {
                user.Id, user.AuthUserId, name = (user.FirstName + " " + user.LastName).Trim(), user.Email,
                companyCode = user.Company!.Code, user.IsActive
            }).ToListAsync(context.RequestAborted);
        return Results.Ok(new { data, page, totalPages = Math.Max(1, (count + pageSize - 1) / pageSize) });
    }

    private static async Task<(int? UserId, IResult? Error)> FindAuthUserIdAsync(
        HttpContext context, IHttpClientFactory clients, string username)
    {
        using var client = clients.CreateClient("AuthApi");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "api/admin/authenticator-recovery/username?username=" + Uri.EscapeDataString(username));
        request.Headers.TryAddWithoutValidation("Authorization", context.Request.Headers.Authorization.ToString());
        try
        {
            using var response = await client.SendAsync(request, context.RequestAborted);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return (null, Results.Forbid());
            if (!response.IsSuccessStatusCode) return (null, Results.StatusCode(503));
            var match = await response.Content.ReadFromJsonAsync<UsernameMatch>(cancellationToken: context.RequestAborted);
            return (match?.AuthUserId, null);
        }
        catch (HttpRequestException) { return (null, Results.StatusCode(503)); }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        { return (null, Results.StatusCode(503)); }
    }

    private static Task<bool> IsBankStaffAsync(HttpContext context, CompGateApiDbContext db)
    {
        var claim = context.User.FindFirstValue("nameid") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) && id > 0
            ? db.Users.AsNoTracking().AnyAsync(user => user.AuthUserId == id && user.CompanyId == null &&
                user.IsActive && user.Role.IsGlobal, context.RequestAborted)
            : Task.FromResult(false);
    }

    private sealed record UsernameMatch(int? AuthUserId);
}
