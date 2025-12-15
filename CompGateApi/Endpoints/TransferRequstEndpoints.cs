using System.Security.Claims;
using System.Text.Json;
using AutoMapper;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompGateApi.Endpoints
{
    public class TransferRequestEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var transfers = app
                .MapGroup("/api/transfers")
                .RequireAuthorization("RequireCompanyUser")
                .WithTags("Transfers");

            transfers.MapGet("/", GetMyTransfers)
                     .Produces<PagedResult<TransferRequestDto>>(200);

            transfers.MapGet("/{id:int}", GetMyTransferById)
                     .Produces<TransferRequestDto>(200)
                     .Produces(404);

            transfers.MapPost("/", CreateTransfer)
                     .Accepts<TransferRequestCreateDto>("application/json")
                     .Produces<TransferRequestDto>(201)
                     .Produces(400)
                     .Produces(401);

            transfers.MapGet("/accounts", LookupAccounts)
                     .WithName("LookupAccounts")
                     .Produces<List<AccountDto>>(200)
                     .Produces(400)
                     .Produces(404);

            transfers.MapGet("/statement", GetStatement)
                     .Produces<List<StatementEntryDto>>(200)
                     .Produces(400);

            var admin = app.MapGroup("/api/admin/transfers")
                           .RequireAuthorization("RequireAdminUser")
                           .WithTags("Transfers");

            admin.MapGet("/", GetAllAdmin)
                 .Produces<PagedResult<TransferRequestDto>>(200);

            admin.MapPut("/{id:int}/status", UpdateStatus)
                 .Accepts<TransferRequestStatusUpdateDto>("application/json")
                 .Produces<TransferRequestDto>(200)
                 .Produces(400)
                 .Produces(404);
            // POST alias for status update
            admin.MapPost("/{id:int}/status/update", UpdateStatus)
                 .Accepts<TransferRequestStatusUpdateDto>("application/json")
                 .Produces<TransferRequestDto>(200)
                 .Produces(400)
                 .Produces(404);

            admin.MapGet("/{id:int}", GetAdminTransferById)
                 .Produces<TransferRequestDto>(200)
                 .Produces(404);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        static bool TryGetAuthUserId(HttpContext ctx, out int userId)
        {
            var raw = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? ctx.User.FindFirstValue("nameid")
                   ?? ctx.User.FindFirstValue("sub"); // ⬅️ Add this line
            return int.TryParse(raw, out userId);
        }

        // ── “My” transfers ──────────────────────────────────────────────────────

        public static async Task<IResult> GetMyTransfers(
            HttpContext ctx,
            ITransferRequestRepository repo,
            IMapper mapper,
            IUserRepository userRepo,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50,
            [FromQuery] string? searchTerm = null)
        {
            if (!TryGetAuthUserId(ctx, out var authId))
                return Results.Unauthorized();

            var me = await userRepo.GetUserByAuthId(authId, ctx.Request.Headers["Authorization"]);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            var total = await repo.GetCountByCompanyAsync(me.CompanyId.Value, searchTerm);
            var list = await repo.GetAllByCompanyAsync(me.CompanyId.Value, searchTerm, page, limit);
            var dtos = list.Select(r => mapper.Map<TransferRequestDto>(r)).ToList();

            return Results.Ok(new PagedResult<TransferRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit)
            });
        }

        public static async Task<IResult> GetMyTransferById(
            int id,
            HttpContext ctx,
            ITransferRequestRepository repo,
            IUserRepository userRepo,
            IMapper mapper)
        {
            if (!TryGetAuthUserId(ctx, out var authId))
                return Results.Unauthorized();

            var token = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, token);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            var ent = await repo.GetByIdAsync(id);
            if (ent == null || ent.CompanyId != me.CompanyId.Value)
                return Results.NotFound();

            return Results.Ok(mapper.Map<TransferRequestDto>(ent));
        }

        // ── Admin single view ───────────────────────────────────────────────────

        public static async Task<IResult> GetAdminTransferById(
            int id,
            HttpContext ctx,
            ITransferRequestRepository repo,
            IUserRepository userRepo,
            IMapper mapper)
        {
            if (!TryGetAuthUserId(ctx, out _))
                return Results.Unauthorized();

            var ent = await repo.GetByIdAsync(id);
            if (ent == null) return Results.NotFound();

            return Results.Ok(mapper.Map<TransferRequestDto>(ent));
        }

        // ── Create a new transfer ───────────────────────────────────────────────

        public static async Task<IResult> CreateTransfer(
         HttpContext ctx,
         [FromBody] TransferRequestCreateDto dto,
         [FromServices] ITransferRequestRepository repo,
         [FromServices] IUserRepository userRepo,
         [FromServices] IValidator<TransferRequestCreateDto> validator,
         [FromServices] IMapper mapper,
         [FromServices] ILogger<TransferRequestEndpoints> log,
         [FromServices] IHttpClientFactory httpFactory,           // kept to preserve signature (unused here)
         [FromServices] CompGateApiDbContext db)                 // kept to preserve signature (unused here)
        {
            // 1) Validate input
            var v = await validator.ValidateAsync(dto);
            if (!v.IsValid)
                return Results.BadRequest(v.Errors.Select(e => e.ErrorMessage));

            // 2) Auth context
            var rawAuthId =
                ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                ctx.User.FindFirstValue("nameid") ??
                ctx.User.FindFirstValue("sub");

            if (!int.TryParse(rawAuthId, out var authId))
                return Results.Unauthorized();

            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me is null || !me.CompanyId.HasValue || me.ServicePackageId <= 0)
                return Results.Unauthorized();

            // 3) Resolve currency from incoming code (CurrencyDesc)
            var code = dto.CurrencyDesc?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code))
                return Results.BadRequest("CurrencyDesc is required.");

            var currency = await db.Currencies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => ((c.Code ?? string.Empty).Trim().ToUpper()) == code);
            if (currency is null)
            {
                var available = await db.Currencies.AsNoTracking()
                                   .Select(c => c.Code)
                                   .ToListAsync();
                return Results.BadRequest($"Unknown currency code '{dto.CurrencyDesc}'. Available: {string.Join(", ", available)}");
            }

            dto.CurrencyId = currency.Id;

            // 4) Call repo (use concrete type to access the new CreateAsync overload)
            if (repo is not CompGateApi.Data.Repositories.TransferRequestRepository concreteRepo)
            {
                log.LogError("TransferRequestRepository concrete implementation not available.");
                return Results.StatusCode(500);
            }

            var result = await concreteRepo.CreateAsync(
                userId: me.UserId,
                companyId: me.CompanyId.Value,
                servicePackageId: me.ServicePackageId,
                dto: dto,
                bearer: bearer,
                ct: ctx.RequestAborted);

            if (!result.Success)
                return Results.BadRequest(result.Error);

            var dtoResult = mapper.Map<TransferRequestDto>(result.Entity!);

            return Results.Ok(new
            {
                message = "✅ Transfer successful",
                transfer = dtoResult,
                totalTakenFromSender = result.SenderTotal,
                totalReceivedByRecipient = result.ReceiverTotal,
                commission = result.Commission.ToString("0.000"),
                limits = new
                {
                    globalLimit = result.GlobalLimit,
                    dailyLimit = result.DailyLimit,
                    usedToday = result.UsedToday.ToString("0.000"),
                    monthlyLimit = result.MonthlyLimit,
                    usedThisMonth = result.UsedThisMonth.ToString("0.000")
                }
            });
        }

        // ── Lookups ─────────────────────────────────────────────────────────────

        public static async Task<IResult> LookupAccounts(
            [FromQuery] string account,
            ITransferRequestRepository repo)
        {
            if (account.Length != 6 && account.Length != 13)
                return Results.BadRequest("Account must be 13 digit.");

            var all = await repo.GetAccountsAsync(account);
            if (account.Length == 6)
                return Results.Ok(all);

            var match = all.Where(a => a.AccountString == account).ToList();
            return match.Count == 0
                ? Results.NotFound($"Account {account} not found.")
                : Results.Ok(match);
        }

        public static async Task<IResult> GetStatement(
            [FromQuery] string account,
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            ITransferRequestRepository repo)
        {
            if (fromDate > toDate)
                return Results.BadRequest("FromDate must precede ToDate.");

            var stm = await repo.GetStatementAsync(account, fromDate, toDate);
            return Results.Ok(stm);
        }

        // ── Admin list & status ────────────────────────────────────────────────

        public static async Task<IResult> GetAllAdmin(
            ITransferRequestRepository repo,
            IMapper mapper,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50,
            [FromQuery] string? searchTerm = null)
        {
            var total = await repo.GetCountAsync(searchTerm);
            var list = await repo.GetAllAsync(searchTerm, page, limit);
            var dtos = list.Select(r => mapper.Map<TransferRequestDto>(r)).ToList();

            return Results.Ok(new PagedResult<TransferRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit)
            });
        }

        public static async Task<IResult> UpdateStatus(
            int id,
            [FromBody] TransferRequestStatusUpdateDto dto,
            ITransferRequestRepository repo,
            IValidator<TransferRequestStatusUpdateDto> validator,
            IMapper mapper,
            ILogger<TransferRequestEndpoints> log)
        {
            var v = await validator.ValidateAsync(dto);
            if (!v.IsValid)
                return Results.BadRequest(v.Errors.Select(e => e.ErrorMessage));

            var ent = await repo.GetByIdAsync(id);
            if (ent == null) return Results.NotFound("Not found.");

            ent.Status = dto.Status;
            await repo.UpdateAsync(ent);
            return Results.Ok(mapper.Map<TransferRequestDto>(ent));
        }
    }
}
