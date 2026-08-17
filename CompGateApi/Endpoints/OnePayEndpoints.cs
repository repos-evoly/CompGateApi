using System.Security.Claims;
using CompGateApi.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Core.OnePay;
using Microsoft.AspNetCore.Mvc;

namespace CompGateApi.Endpoints;

public sealed class OnePayEndpoints : IEndpoints
{
    public void RegisterEndpoints(WebApplication app)
    {
        var onePay = app.MapGroup("/api/onepay")
            .RequireAuthorization("RequireCompanyUser")
            .WithTags("OnePay");

        onePay.MapGet("/accounts", GetAccounts);
        onePay.MapGet("/institutions", GetInstitutions);
        onePay.MapGet("/transfers", GetTransfers);
        onePay.MapGet("/transfers/{id:int}", GetTransfer);
        onePay.MapPost("/transfers/validate", ValidateTransfer);
        onePay.MapPost("/transfers", CreateTransfer);
        onePay.MapPost("/transfers/{id:int}/approve", ApproveAndExecute);
        onePay.MapPost("/transfers/{id:int}/refresh-status", RefreshStatus);
    }

    private static async Task<IResult> GetAccounts(
        HttpContext context,
        OnePayTransferService service)
    {
        var user = await GetUserAsync(context, service);
        if (user is null) return Results.Unauthorized();

        var result = await service.GetAccountsAsync(user, context.RequestAborted);
        return ToResult(result);
    }

    private static async Task<IResult> GetInstitutions(
        HttpContext context,
        OnePayTransferService service,
        [FromQuery] string language = "EN")
    {
        var user = await GetUserAsync(context, service);
        if (user is null) return Results.Unauthorized();

        var result = await service.GetInstitutionsAsync(user, language, context.RequestAborted);
        return ToResult(result);
    }

    private static async Task<IResult> GetTransfers(
        HttpContext context,
        OnePayTransferService service,
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
        OnePayTransferService service)
    {
        var user = await GetUserAsync(context, service);
        if (user is null) return Results.Unauthorized();
        if (!service.CanView(user)) return Results.Forbid();

        var transfer = await service.GetTransferAsync(user, id, context.RequestAborted);
        return transfer is null
            ? Results.NotFound(new { message = "OnePay transfer was not found." })
            : Results.Ok(transfer);
    }

    private static async Task<IResult> ValidateTransfer(
        [FromBody] OnePayValidateTransferRequestDto request,
        HttpContext context,
        OnePayTransferService service)
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
        [FromBody] OnePayCreateTransferRequestDto request,
        HttpContext context,
        OnePayTransferService service)
    {
        var user = await GetUserAsync(context, service);
        if (user is null) return Results.Unauthorized();

        var result = await service.CreateAsync(
            user,
            request.ValidationToken,
            context.RequestAborted);
        if (!result.Success) return ToResult(result);

        return Results.Created(
            $"/api/onepay/transfers/{result.Value!.Id}",
            result.Value);
    }

    private static async Task<IResult> ApproveAndExecute(
        int id,
        HttpContext context,
        OnePayTransferService service)
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
        OnePayTransferService service)
    {
        var user = await GetUserAsync(context, service);
        if (user is null) return Results.Unauthorized();

        var result = await service.RefreshStatusAsync(
            user,
            id,
            context.RequestAborted);
        return ToResult(result);
    }

    private static async Task<OnePayCurrentUser?> GetUserAsync(
        HttpContext context,
        OnePayTransferService service)
    {
        var raw = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("nameid")
            ?? context.User.FindFirstValue("sub");
        return int.TryParse(raw, out var authUserId)
            ? await service.GetCurrentUserAsync(authUserId, context.RequestAborted)
            : null;
    }

    private static IResult ToResult<T>(OnePayServiceResult<T> result)
    {
        if (result.Success) return Results.Ok(result.Value);

        var payload = new { message = result.Error ?? "OnePay request failed." };
        return result.FailureKind switch
        {
            OnePayFailureKind.Forbidden => Results.Json(payload, statusCode: StatusCodes.Status403Forbidden),
            OnePayFailureKind.NotFound => Results.NotFound(payload),
            OnePayFailureKind.Conflict => Results.Conflict(payload),
            OnePayFailureKind.Unavailable => Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.BadRequest(payload)
        };
    }
}
