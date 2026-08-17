using System.Security.Claims;
using CompGateApi.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Core.LyPay;
using Microsoft.AspNetCore.Mvc;

namespace CompGateApi.Endpoints;

public sealed class LyPayEndpoints : IEndpoints
{
    public void RegisterEndpoints(WebApplication app)
    {
        var lyPay = app.MapGroup("/api/lypay")
            .RequireAuthorization("RequireCompanyUser")
            .WithTags("LyPay");

        lyPay.MapGet("/accounts", GetAccounts);
        lyPay.MapGet("/institutions", GetInstitutions);
        lyPay.MapGet("/transfers", GetTransfers);
        lyPay.MapGet("/transfers/{id:int}", GetTransfer);
        lyPay.MapPost("/transfers/validate", ValidateTransfer);
        lyPay.MapPost("/transfers", CreateTransfer);
        lyPay.MapPost("/transfers/{id:int}/approve", ApproveAndExecute);
        lyPay.MapPost("/transfers/{id:int}/refresh-status", RefreshStatus);
    }

    private static async Task<IResult> GetAccounts(
        HttpContext context,
        LyPayTransferService service)
    {
        var user = await GetUserAsync(context, service);
        if (user is null) return Results.Unauthorized();

        var result = await service.GetAccountsAsync(user, context.RequestAborted);
        return ToResult(result);
    }

    private static async Task<IResult> GetInstitutions(
        HttpContext context,
        LyPayTransferService service,
        [FromQuery] string language = "EN")
    {
        var user = await GetUserAsync(context, service);
        if (user is null) return Results.Unauthorized();

        var result = await service.GetInstitutionsAsync(user, language, context.RequestAborted);
        return ToResult(result);
    }

    private static async Task<IResult> GetTransfers(
        HttpContext context,
        LyPayTransferService service,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? searchTerm = null)
    {
        var user = await GetUserAsync(context, service);
        if (user is null) return Results.Unauthorized();
        if (!service.CanView(user)) return Results.Forbid();

        var result = await service.GetTransfersAsync(
            user,
            page,
            limit,
            searchTerm,
            context.RequestAborted);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTransfer(
        int id,
        HttpContext context,
        LyPayTransferService service)
    {
        var user = await GetUserAsync(context, service);
        if (user is null) return Results.Unauthorized();
        if (!service.CanView(user)) return Results.Forbid();

        var transfer = await service.GetTransferAsync(user, id, context.RequestAborted);
        return transfer is null
            ? Results.NotFound(new { message = "LyPay transfer was not found." })
            : Results.Ok(transfer);
    }

    private static async Task<IResult> ValidateTransfer(
        [FromBody] LyPayValidateTransferRequestDto request,
        HttpContext context,
        LyPayTransferService service)
    {
        var user = await GetUserAsync(context, service);
        if (user is null) return Results.Unauthorized();

        var deviceId = context.Request.Headers["X-CompGate-Device-Id"].FirstOrDefault();
        var userAgent = context.Request.Headers.UserAgent.FirstOrDefault();
        var result = await service.ValidateAsync(
            user,
            request,
            deviceId,
            userAgent,
            context.RequestAborted);
        return ToResult(result);
    }

    private static async Task<IResult> CreateTransfer(
        [FromBody] LyPayCreateTransferRequestDto request,
        HttpContext context,
        LyPayTransferService service)
    {
        var user = await GetUserAsync(context, service);
        if (user is null) return Results.Unauthorized();

        var result = await service.CreateAsync(
            user,
            request.ValidationToken,
            context.RequestAborted);
        if (!result.Success) return ToResult(result);

        return Results.Created(
            $"/api/lypay/transfers/{result.Value!.Id}",
            result.Value);
    }

    private static async Task<IResult> ApproveAndExecute(
        int id,
        HttpContext context,
        LyPayTransferService service)
    {
        var user = await GetUserAsync(context, service);
        if (user is null) return Results.Unauthorized();

        var result = await service.ApproveAndExecuteAsync(
            user,
            id,
            context.RequestAborted);
        return ToResult(result);
    }

    private static async Task<IResult> RefreshStatus(
        int id,
        HttpContext context,
        LyPayTransferService service)
    {
        var user = await GetUserAsync(context, service);
        if (user is null) return Results.Unauthorized();

        var result = await service.RefreshStatusAsync(
            user,
            id,
            context.RequestAborted);
        return ToResult(result);
    }

    private static async Task<LyPayCurrentUser?> GetUserAsync(
        HttpContext context,
        LyPayTransferService service)
    {
        var raw = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("nameid")
            ?? context.User.FindFirstValue("sub");
        return int.TryParse(raw, out var authUserId)
            ? await service.GetCurrentUserAsync(authUserId, context.RequestAborted)
            : null;
    }

    private static IResult ToResult<T>(LyPayServiceResult<T> result)
    {
        if (result.Success) return Results.Ok(result.Value);

        var payload = new { message = result.Error ?? "LyPay request failed." };
        return result.FailureKind switch
        {
            LyPayFailureKind.Forbidden => Results.Json(payload, statusCode: StatusCodes.Status403Forbidden),
            LyPayFailureKind.NotFound => Results.NotFound(payload),
            LyPayFailureKind.Conflict => Results.Conflict(payload),
            LyPayFailureKind.Unavailable => Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.BadRequest(payload)
        };
    }
}
