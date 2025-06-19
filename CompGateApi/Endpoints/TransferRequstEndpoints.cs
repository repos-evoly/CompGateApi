using System.Security.Claims;
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
                log.LogInformation("Starting CreateTransfer with FromAccount={FromAccount} ToAccount={ToAccount}", dto.FromAccount, dto.ToAccount);

                // Validate DTO
                var validation = await validator.ValidateAsync(dto);
                if (!validation.IsValid)
                {
                    log.LogWarning("Validation failed: {Errors}", string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)));
                    return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));
                }

                // Get Authenticated User
                var token = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var raw = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? ctx.User.FindFirstValue("nameid")
                    ?? ctx.User.FindFirstValue("sub");

                if (!int.TryParse(raw, out var authId))
                {
                    log.LogWarning("Unauthorized: invalid or missing authId claim");
                    return Results.Unauthorized();
                }

                var me = await userRepo.GetUserByAuthId(authId, token);
                if (me == null || !me.CompanyId.HasValue)
                {
                    log.LogWarning("Unauthorized: user not found or missing CompanyId");
                    return Results.Unauthorized();
                }

                var companyId = me.CompanyId.Value;
                var pkgId = me.ServicePackageId;
                log.LogInformation("Authenticated user: {UserId}, CompanyId: {CompanyId}, PackageId: {PackageId}", me.UserId, companyId, pkgId);

                if (pkgId <= 0)
                {
                    log.LogWarning("Invalid service package ID");
                    return Results.BadRequest("Your company does not have a service package configured.");
                }

                // Get STCOD from receiver
                string? stcod = null;
                try
                {
                    stcod = await repo.GetStCodeByAccount(dto.ToAccount);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Failed to fetch STCOD from account: {ToAccount}", dto.ToAccount);
                    return Results.BadRequest("Unable to determine transfer type from receiver account.");
                }

                if (string.IsNullOrWhiteSpace(stcod))
                {
                    log.LogWarning("STCOD is empty or null for account: {ToAccount}", dto.ToAccount);
                    return Results.BadRequest("Unable to determine transfer type from receiver account.");
                }

                var isB2B = stcod == "CD";
                var transferMode = isB2B ? "B2B" : "B2C";

                // Currency + rate
                var currency = await db.Currencies.FindAsync(dto.CurrencyId);
                if (currency == null)
                {
                    log.LogWarning("Invalid currency selected: {CurrencyId}", dto.CurrencyId);
                    return Results.BadRequest("Invalid currency selected.");
                }

                var rate = currency.Rate;
                var amountInBase = dto.Amount * rate;
                log.LogInformation("Currency validated. Rate={Rate}, BaseAmount={AmountInBase}", rate, amountInBase);

                // Global system-wide limit
                var settings = await db.Settings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    log.LogError("Global settings not found in DB");
                    return Results.BadRequest("System configuration missing.");
                }

                if (amountInBase > settings.GlobalLimit)
                {
                    log.LogWarning("Global limit exceeded: {AmountInBase} > {GlobalLimit}", amountInBase, settings.GlobalLimit);
                    return Results.BadRequest($"Global limit of {settings.GlobalLimit} exceeded.");
                }

                // Service package + details
                var servicePackage = await db.ServicePackages.FindAsync(pkgId);
                if (servicePackage == null)
                {
                    log.LogWarning("Service package not found: {PackageId}", pkgId);
                    return Results.BadRequest("Invalid service package.");
                }

                var detail = await db.ServicePackageDetails
                    .Include(d => d.TransactionCategory)
                    .FirstOrDefaultAsync(d => d.ServicePackageId == pkgId && d.TransactionCategoryId == dto.TransactionCategoryId);

                if (detail == null || !detail.IsEnabledForPackage)
                {
                    log.LogWarning("Category not enabled for package: {TransactionCategoryId}", dto.TransactionCategoryId);
                    return Results.BadRequest("This category is not enabled in your service package.");
                }

                // Per-transaction limit
                var transactionLimit = isB2B ? detail.B2BTransactionLimit : detail.B2CTransactionLimit;
                if (transactionLimit.HasValue && amountInBase > transactionLimit.Value)
                {
                    log.LogWarning("Transaction limit exceeded: {AmountInBase} > {Limit}", amountInBase, transactionLimit.Value);
                    return Results.BadRequest($"Transaction limit exceeded ({transactionLimit.Value}).");
                }

                // Daily limit
                var today = DateTime.UtcNow.Date;
                var dailyBaseSum = await db.TransferRequests
                    .Where(t => t.CompanyId == companyId && t.RequestedAt.Date == today)
                    .Select(t => t.Amount * t.Rate)
                    .SumAsync();

                if (dailyBaseSum + amountInBase > servicePackage.DailyLimit)
                {
                    log.LogWarning("Daily limit exceeded: {Sum} + {AmountInBase} > {Limit}", dailyBaseSum, amountInBase, servicePackage.DailyLimit);
                    return Results.BadRequest($"Daily limit of {servicePackage.DailyLimit} exceeded.");
                }

                // Monthly limit
                var monthStart = new DateTime(today.Year, today.Month, 1);
                var monthlyBaseSum = await db.TransferRequests
                    .Where(t => t.CompanyId == companyId && t.RequestedAt >= monthStart)
                    .Select(t => t.Amount * t.Rate)
                    .SumAsync();

                if (monthlyBaseSum + amountInBase > servicePackage.MonthlyLimit)
                {
                    log.LogWarning("Monthly limit exceeded: {Sum} + {AmountInBase} > {Limit}", monthlyBaseSum, amountInBase, servicePackage.MonthlyLimit);
                    return Results.BadRequest($"Monthly limit of {servicePackage.MonthlyLimit} exceeded.");
                }

                // Commission Calculation
                var fixedFee = isB2B ? detail.B2BFixedFee ?? 0m : detail.B2CFixedFee ?? 0m;
                var pct = isB2B ? detail.B2BCommissionPct ?? 0m : detail.B2CCommissionPct ?? 0m;
                var pctFee = dto.Amount * (pct / 100m);
                var commission = Math.Max(fixedFee, pctFee);
                commission = decimal.Round(commission, 3);

                log.LogInformation("Commission calculated: Fixed={Fixed}, Percent={Percent}, Result={Commission}", fixedFee, pctFee, commission);

                // Bank API Call
                var bankPayload = new
                {
                    Header = new
                    {
                        system = "MOBILE",
                        referenceId = Guid.NewGuid().ToString("N").Substring(0, 16),
                        userName = "TEDMOB",
                        customerNumber = dto.FromAccount,
                        requestTime = DateTime.UtcNow.ToString("o"),
                        language = "AR"
                    },
                    Details = new Dictionary<string, string>
                    {
                        ["@SRCACC"] = dto.FromAccount,
                        ["@DSTACC"] = dto.ToAccount,
                        ["@APLYTRN2"] = "Y",
                        ["@TRFAMT"] = ((long)((dto.Amount - commission) * 1000m)).ToString("D15"),
                        ["@NR2"] = dto.Description ?? ""
                    }
                };

                var bankClient = httpFactory.CreateClient("BankApi");
                var response = await bankClient.PostAsJsonAsync("/api/mobile/postTransfer", bankPayload);
                if (!response.IsSuccessStatusCode)
                {
                    log.LogError("Bank API failed. Status={Status}, Content={Content}", response.StatusCode, await response.Content.ReadAsStringAsync());
                    return Results.BadRequest("Bank transfer failed.");
                }

                // Validate Economic Sector
                if (dto.EconomicSectorId != 0)
                {
                    var exists = await db.EconomicSectors.AnyAsync(e => e.Id == dto.EconomicSectorId);
                    if (!exists)
                    {
                        log.LogWarning("Invalid economic sector: {EconomicSectorId}", dto.EconomicSectorId);
                        return Results.BadRequest("Invalid Economic Sector.");
                    }
                }

                // Save
                var transfer = new TransferRequest
                {
                    UserId = me.UserId,
                    CompanyId = companyId,
                    TransactionCategoryId = dto.TransactionCategoryId,
                    FromAccount = dto.FromAccount,
                    ToAccount = dto.ToAccount,
                    Amount = dto.Amount,
                    CurrencyId = dto.CurrencyId,
                    ServicePackageId = pkgId,
                    Status = "Completed",
                    Description = dto.Description,
                    RequestedAt = DateTime.UtcNow,
                    CommissionAmount = commission,
                    CommissionOnRecipient = dto.CommissionOnRecipient,
                    Rate = rate,
                    TransferMode = transferMode,
                    EconomicSectorId = dto.EconomicSectorId,
                };

                await repo.CreateAsync(transfer);
                log.LogInformation("Transfer created successfully: {TransferId}", transfer.Id);

                var resultDto = mapper.Map<TransferRequestDto>(transfer);
                return Results.Created($"/api/transfers/{transfer.Id}", resultDto);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Unhandled exception in CreateTransfer");
                return Results.StatusCode(500);
            }
        }



        // ── Lookups ─────────────────────────────────────────────────────────────

        public static async Task<IResult> LookupAccounts(
            [FromQuery] string account,
            ITransferRequestRepository repo)
        {
            if (account.Length != 6 && account.Length != 13)
                return Results.BadRequest("Account must be 6-digit code or 13-digit account.");

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
