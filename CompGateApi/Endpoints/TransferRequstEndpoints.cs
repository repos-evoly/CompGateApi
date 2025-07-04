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
    [FromServices] IHttpClientFactory httpFactory,
    [FromServices] CompGateApiDbContext db)
        {
            try
            {
                log.LogInformation("🔁 CreateTransfer started");

                var validation = await validator.ValidateAsync(dto);
                if (!validation.IsValid)
                {
                    var errors = validation.Errors.Select(e => e.ErrorMessage);
                    log.LogWarning("❌ Validation failed: {Errors}", string.Join(", ", errors));
                    return Results.BadRequest(errors);
                }

                var token = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var rawAuthId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                                  ?? ctx.User.FindFirstValue("nameid")
                                  ?? ctx.User.FindFirstValue("sub");

                if (!int.TryParse(rawAuthId, out var authId))
                {
                    log.LogWarning("❌ Missing or invalid auth ID");
                    return Results.Unauthorized();
                }

                var me = await userRepo.GetUserByAuthId(authId, token);
                if (me == null || !me.CompanyId.HasValue)
                    return Results.Unauthorized();

                var companyId = me.CompanyId.Value;
                var pkgId = me.ServicePackageId;
                if (pkgId <= 0)
                    return Results.BadRequest("No service package configured");

                var stcod = await repo.GetStCodeByAccount(dto.ToAccount);
                if (string.IsNullOrWhiteSpace(stcod))
                    return Results.BadRequest("Receiver account type unknown");

                bool isB2B = stcod == "CD";
                string transferMode = isB2B ? "B2B" : "B2C";

                var currency = await db.Currencies.FindAsync(dto.CurrencyId);
                if (currency == null)
                    return Results.BadRequest("Invalid currency");

                decimal rate = currency.Rate;
                decimal amountInBase = dto.Amount * rate;

                var settings = await db.Settings.FirstOrDefaultAsync();
                if (settings == null)
                    return Results.BadRequest("System settings missing");



                if (amountInBase > settings.GlobalLimit)
                    return Results.BadRequest("Global limit exceeded");

                var detail = await db.ServicePackageDetails
                    .Include(d => d.TransactionCategory)
                    .FirstOrDefaultAsync(d =>
                        d.ServicePackageId == pkgId &&
                        d.TransactionCategoryId == dto.TransactionCategoryId);

                if (detail == null || !detail.IsEnabledForPackage)
                    return Results.BadRequest("Internal Transfer not allowed");

                decimal? txnLimit = isB2B ? detail.B2BTransactionLimit : detail.B2CTransactionLimit;
                if (txnLimit.HasValue && amountInBase > txnLimit.Value)
                    return Results.BadRequest("Transaction limit exceeded");

                var today = DateTime.UtcNow.Date;
                var monthStart = new DateTime(today.Year, today.Month, 1);

                var todayTotal = await db.TransferRequests
                    .Where(t => t.CompanyId == companyId && t.RequestedAt.Date == today)
                    .Select(t => t.Amount * t.Rate).SumAsync();

                var monthTotal = await db.TransferRequests
                    .Where(t => t.CompanyId == companyId && t.RequestedAt >= monthStart)
                    .Select(t => t.Amount * t.Rate).SumAsync();

                var pkg = await db.ServicePackages.FindAsync(pkgId);
                if (todayTotal + amountInBase > pkg!.DailyLimit)
                    return Results.BadRequest("Daily limit exceeded");
                if (monthTotal + amountInBase > pkg.MonthlyLimit)
                    return Results.BadRequest("Monthly limit exceeded");

                decimal fixedFee = isB2B ? detail.B2BFixedFee ?? 0 : detail.B2CFixedFee ?? 0;
                decimal pct = isB2B ? detail.B2BCommissionPct ?? 0 : detail.B2CCommissionPct ?? 0;
                decimal pctFee = dto.Amount * (pct / 100m);
                decimal commission = Math.Round(Math.Max(fixedFee, pctFee), 3);

                string currencyCode = currency.Id switch
                {
                    1 => "LYD",
                    2 => "USD",
                    3 => "EUR",
                    _ => "LYD"
                };

                const int DECIMALS = 3;
                decimal scale = (decimal)Math.Pow(10, DECIMALS);
                string amountStr = ((long)(dto.Amount * scale)).ToString("D15");
                string commStr = ((long)(commission * scale)).ToString("D15");

                string senderTotal = dto.CommissionOnRecipient
                    ? dto.Amount.ToString("0.000")
                    : (dto.Amount + commission).ToString("0.000");

                string receiverTotal = dto.CommissionOnRecipient
                    ? (dto.Amount - commission).ToString("0.000")
                    : dto.Amount.ToString("0.000");

                string commissionAccount = currencyCode == "USD"
                    ? settings.CommissionAccountUSD
                    : settings.CommissionAccount;

                var payload = new
                {
                    Header = new
                    {
                        system = "MOBILE",
                        referenceId = Guid.NewGuid().ToString("N")[..16],
                        userName = "TEDMOB",
                        customerNumber = dto.ToAccount,
                        requestTime = DateTime.UtcNow.ToString("o"),
                        language = "AR"
                    },
                    Details = new Dictionary<string, string>
                    {
                        ["@TRFCCY"] = currencyCode,
                        ["@SRCACC"] = dto.FromAccount,
                        ["@SRCACC2"] = dto.CommissionOnRecipient ? dto.ToAccount : dto.FromAccount,
                        ["@DSTACC"] = dto.ToAccount,
                        ["@DSTACC2"] = commissionAccount,
                        ["@TRFAMT"] = amountStr,
                        ["@APLYTRN2"] = "Y",
                        ["@TRFAMT2"] = commStr,
                        ["@NR2"] = dto.Description ?? ""
                    }
                };

                log.LogInformation("📤 Bank payload: {Payload}", JsonSerializer.Serialize(payload));

                var httpClient = httpFactory.CreateClient();
                var response = await httpClient.PostAsJsonAsync("http://10.3.3.11:7070/api/mobile/flexPostTransfer", payload);
                var bankRaw = await response.Content.ReadAsStringAsync();

                log.LogInformation("📥 Bank response: {Raw}", bankRaw);

                if (!response.IsSuccessStatusCode)
                    return Results.BadRequest("Bank error: " + response.StatusCode);

                var bankRes = JsonSerializer.Deserialize<BankResponseDto>(bankRaw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (bankRes?.Header?.ReturnCode?.ToLower() != "success")
                    return Results.BadRequest("Bank rejected: " + bankRes?.Header?.ReturnMessage);

                var entity = new TransferRequest
                {
                    UserId = me.UserId,
                    CompanyId = companyId,
                    TransactionCategoryId = dto.TransactionCategoryId,
                    FromAccount = dto.FromAccount,
                    ToAccount = dto.ToAccount,
                    Amount = dto.Amount,
                    CurrencyId = dto.CurrencyId,
                    ServicePackageId = pkgId,
                    Description = dto.Description,
                    RequestedAt = DateTime.UtcNow,
                    Status = "Completed",
                    EconomicSectorId = dto.EconomicSectorId,
                    CommissionAmount = commission,
                    CommissionOnRecipient = dto.CommissionOnRecipient,
                    Rate = rate,
                    TransferMode = transferMode
                };

                await repo.CreateAsync(entity);
                var dtoResult = mapper.Map<TransferRequestDto>(entity);

                return Results.Ok(new
                {
                    message = "✅ Transfer successful",
                    transfer = dtoResult,
                    totalTakenFromSender = senderTotal,
                    totalReceivedByRecipient = receiverTotal,
                    commission = commission.ToString("0.000"),
                    limits = new
                    {
                        globalLimit = settings.GlobalLimit,
                        dailyLimit = pkg.DailyLimit,
                        usedToday = (todayTotal + amountInBase).ToString("0.000"),
                        monthlyLimit = pkg.MonthlyLimit,
                        usedThisMonth = (monthTotal + amountInBase).ToString("0.000")
                    }
                });
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Unhandled error in CreateTransfer");
                return Results.StatusCode(500);
            }
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
