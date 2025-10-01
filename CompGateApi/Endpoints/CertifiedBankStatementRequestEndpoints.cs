// ─────────────────────────────────────────────────────────────────────────────
// CompGateApi.Endpoints/CertifiedBankStatementRequestEndpoints.cs
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompGateApi.Endpoints
{
    public class CertifiedBankStatementRequestEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            // ── COMPANY ROUTES ────────────────────────────────────────────────
            var company = app
                .MapGroup("/api/certifiedbankstatementrequests")
                .WithTags("CertifiedBankStatements")
                .RequireAuthorization("RequireCompanyUser");
            // .RequireAuthorization("CanRequestBankStatement");

            company.MapGet("/", GetCompanyRequests)
                   .Produces<PagedResult<CertifiedBankStatementRequestDto>>(200);

            company.MapGet("/{id:int}", GetCompanyRequestById)
                   .Produces<CertifiedBankStatementRequestDto>(200)
                   .Produces(404);

            company.MapPost("/", CreateCompanyRequest)
                   .Accepts<CertifiedBankStatementRequestCreateDto>("application/json")
                   .Produces<CertifiedBankStatementRequestDto>(201)
                   .Produces(400);

            company.MapPut("/{id:int}", UpdateCompanyRequest)
                   .Accepts<CertifiedBankStatementRequestCreateDto>("application/json")
                   .Produces<CertifiedBankStatementRequestDto>(200)
                   .Produces(400)
                   .Produces(404);

            // ── ADMIN ROUTES ──────────────────────────────────────────────────
            var admin = app
                .MapGroup("/api/admin/certifiedbankstatementrequests")
                .WithTags("CertifiedBankStatements")
                .RequireAuthorization("RequireAdminUser");
            // .RequireAuthorization("AdminAccess");

            admin.MapGet("/", AdminGetAll)
                 .Produces<PagedResult<CertifiedBankStatementRequestDto>>(200);

            admin.MapPut("/{id:int}/status", AdminUpdateStatus)
                 .Accepts<CertifiedBankStatementRequestStatusUpdateDto>("application/json")
                 .Produces<CertifiedBankStatementRequestDto>(200)
                 .Produces(400)
                 .Produces(404);

            admin.MapGet("/{id:int}", AdminGetById)
                 .Produces<CertifiedBankStatementRequestDto>(200)
                 .Produces(404);
        }

        private const int TRXCAT_CERT_STMT = 9; // ← change to your actual id
        private const string DEFAULT_CCY = "LYD";

        // ── helpers ─────────────────────────────────────────────────────────
        private static int GetAuthUserId(HttpContext ctx)
        {
            var raw = ctx.User.FindFirst("nameid")?.Value
                   ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(raw, out var id)) return id;
            throw new UnauthorizedAccessException("Missing/invalid 'nameid' claim.");
        }

        /// <summary>Bank core expects 13-digit account with leading zeros.</summary>
        private static string NormalizeAcc13(long acc) => acc.ToString().PadLeft(13, '0');

        /// <summary>Replace {BRANCH} with first 4 digits of the source account.</summary>
        private static string ResolveGlWithBranch(string gl1, string fromAccount)
        {
            if (string.IsNullOrWhiteSpace(gl1))
                throw new ArgumentException("Pricing.GL1 is not configured.");
            if (string.IsNullOrWhiteSpace(fromAccount) || fromAccount.Length < 4)
                throw new ArgumentException("Invalid account for branch extraction.");
            var branch = fromAccount.Substring(0, 4);
            return gl1.Replace("{BRANCH}", branch, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Single place that maps entity → DTO (includes StatementRequest.FromDate/ToDate).</summary>
        private static CertifiedBankStatementRequestDto ToDto(CertifiedBankStatementRequest r)
        {
            return new CertifiedBankStatementRequestDto
            {
                Id = r.Id,
                UserId = r.UserId,
                CompanyId = r.CompanyId,

                AccountHolderName = r.AccountHolderName,
                AuthorizedOnTheAccountName = r.AuthorizedOnTheAccountName,
                AccountNumber = r.AccountNumber,
                OldAccountNumber = r.OldAccountNumber,
                NewAccountNumber = r.NewAccountNumber,

                TotalAmountLyd = r.TotalAmountLyd,

                ServiceRequests = r.ServiceRequests == null ? null : new ServicesRequestDto
                {
                    ReactivateIdfaali = r.ServiceRequests.ReactivateIdfaali,
                    DeactivateIdfaali = r.ServiceRequests.DeactivateIdfaali,
                    ResetDigitalBankPassword = r.ServiceRequests.ResetDigitalBankPassword,
                    ResendMobileBankingPin = r.ServiceRequests.ResendMobileBankingPin,
                    ChangePhoneNumber = r.ServiceRequests.ChangePhoneNumber
                },

                StatementRequest = r.StatementRequest == null ? null : new StatementRequestDto
                {
                    CurrentAccountStatementArabic = r.StatementRequest.CurrentAccountStatementArabic,
                    CurrentAccountStatementEnglish = r.StatementRequest.CurrentAccountStatementEnglish,
                    VisaAccountStatement = r.StatementRequest.VisaAccountStatement,
                    AccountStatement = r.StatementRequest.AccountStatement,
                    JournalMovement = r.StatementRequest.JournalMovement,
                    NonFinancialCommitment = r.StatementRequest.NonFinancialCommitment,
                    FromDate = r.StatementRequest.FromDate,
                    ToDate = r.StatementRequest.ToDate
                },

                Status = r.Status,
                Reason = r.Reason,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            };
        }

        // ── COMPANY: list by company ─────────────────────────────────────
        public static async Task<IResult> GetCompanyRequests(
            HttpContext ctx,
            ICertifiedBankStatementRequestRepository repo,
            IUserRepository userRepo,
            ILogger<CertifiedBankStatementRequestEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            var total = await repo.GetCountByCompanyAsync(me.CompanyId.Value, searchTerm, searchBy);
            var list = await repo.GetAllByCompanyAsync(me.CompanyId.Value, searchTerm, searchBy, page, limit);

            var dtos = list.Select(ToDto).ToList();

            return Results.Ok(new PagedResult<CertifiedBankStatementRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit)
            });
        }

        // ── COMPANY: get single ─────────────────────────────────────────
        public static async Task<IResult> GetCompanyRequestById(
            int id,
            HttpContext ctx,
            ICertifiedBankStatementRequestRepository repo,
            IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            var ent = await repo.GetByIdAsync(id);
            if (ent == null || ent.CompanyId != me.CompanyId.Value)
                return Results.NotFound();

            return Results.Ok(ToDto(ent));
        }

        // ── COMPANY: create ─────────────────────────────────────────────
        public static async Task<IResult> CreateCompanyRequest(
            [FromBody] CertifiedBankStatementRequestCreateDto dto,
            HttpContext ctx,
            ICertifiedBankStatementRequestRepository repo,
            IUserRepository userRepo,
            ILogger<CertifiedBankStatementRequestEndpoints> log,
            [FromServices] CompGateApiDbContext db,
            [FromServices] IGenericTransferRepository genericTransferRepo)
        {
            try
            {
                var authId = GetAuthUserId(ctx);
                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null || !me.CompanyId.HasValue)
                    return Results.Unauthorized();

                if (dto.TotalAmountLyd <= 0m)
                    return Results.BadRequest("TotalAmountLyd must be > 0.");

                // 1) Persist the form first (Pending)
                var ent = new CertifiedBankStatementRequest
                {
                    CompanyId = me.CompanyId.Value,
                    UserId = me.UserId,
                    AccountHolderName = dto.AccountHolderName,
                    AuthorizedOnTheAccountName = dto.AuthorizedOnTheAccountName,
                    AccountNumber = dto.AccountNumber,
                    ServiceRequests = new ServicesRequest
                    {
                        ReactivateIdfaali = dto.ServiceRequests?.ReactivateIdfaali ?? false,
                        DeactivateIdfaali = dto.ServiceRequests?.DeactivateIdfaali ?? false,
                        ResetDigitalBankPassword = dto.ServiceRequests?.ResetDigitalBankPassword ?? false,
                        ResendMobileBankingPin = dto.ServiceRequests?.ResendMobileBankingPin ?? false,
                        ChangePhoneNumber = dto.ServiceRequests?.ChangePhoneNumber ?? false
                    },
                    StatementRequest = new StatementRequest
                    {
                        CurrentAccountStatementArabic = dto.StatementRequest?.CurrentAccountStatementArabic,
                        CurrentAccountStatementEnglish = dto.StatementRequest?.CurrentAccountStatementEnglish,
                        VisaAccountStatement = dto.StatementRequest?.VisaAccountStatement,
                        AccountStatement = dto.StatementRequest?.AccountStatement,
                        JournalMovement = dto.StatementRequest?.JournalMovement,
                        NonFinancialCommitment = dto.StatementRequest?.NonFinancialCommitment,
                        FromDate = dto.StatementRequest?.FromDate,
                        ToDate = dto.StatementRequest?.ToDate
                    },
                    OldAccountNumber = dto.OldAccountNumber,
                    NewAccountNumber = dto.NewAccountNumber,
                    TotalAmountLyd = dto.TotalAmountLyd,
                    Status = "Pending",
                    Reason = string.Empty
                };
                await repo.CreateAsync(ent);
                log.LogInformation("Created CertifiedBankStatementRequest Id={Id}", ent.Id);

                // 2) Load pricing for certified statement (TrxCatId=9, Unit=31)
                var pricing = await db.Pricings.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.TrxCatId == TRXCAT_CERT_STMT && p.Unit == 31);
                if (pricing == null)
                    return Results.BadRequest($"Pricing not configured (TrxCatId={TRXCAT_CERT_STMT}, Unit=31).");

                // 3) Build source / destination accounts
                var srcAcc13 = NormalizeAcc13(dto.AccountNumber);
                string toAccount;
                try
                {
                    toAccount = ResolveGlWithBranch(pricing.GL1, srcAcc13);
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Resolve GL1 failed for CertifiedBankStatement create.");
                    return Results.BadRequest(ex.Message);
                }

                // 4) Narrative (prefer NR2)
                var narrative = string.IsNullOrWhiteSpace(pricing.NR2)
                    ? "Certified Bank Statement fee"
                    : pricing.NR2;

                // 5) Debit now
                var debit = await genericTransferRepo.DebitForServiceAsync(
                    userId: me.UserId,
                    companyId: me.CompanyId.Value,
                    servicePackageId: me.ServicePackageId,
                    trxCategoryId: TRXCAT_CERT_STMT,
                    fromAccount: srcAcc13,
                    toAccount: toAccount,
                    amount: dto.TotalAmountLyd,
                    description: narrative,
                    currencyCode: DEFAULT_CCY,
                    dtc: pricing.DTC,
                    ctc: pricing.CTC,
                    dtc2: pricing.DTC2,
                    ctc2: pricing.CTC2,
                    applySecondLeg: pricing.APPLYTR2,
                    narrativeOverride: narrative
                );

                if (!debit.Success)
                    return Results.BadRequest(debit.Error);

                // 6) Link transfer & bank ref, update entity
                ent.TransferRequestId = debit.Entity!.Id;
                ent.BankReference = debit.BankReference;
                await repo.UpdateAsync(ent);

                return Results.Created($"/api/certifiedbankstatementrequests/{ent.Id}", ToDto(ent));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        }

        // ── COMPANY: update ─────────────────────────────────────────────
        public static async Task<IResult> UpdateCompanyRequest(
            int id,
            [FromBody] CertifiedBankStatementRequestCreateDto dto,
            HttpContext ctx,
            ICertifiedBankStatementRequestRepository repo,
            IUserRepository userRepo,
            IValidator<CertifiedBankStatementRequestCreateDto> validator,
            ILogger<CertifiedBankStatementRequestEndpoints> log)
        {
            log.LogInformation("UpdateCompanyRequest payload: {@Dto}", dto);

            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                log.LogWarning("Validation failed: {Errors}", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));
            }

            try
            {
                var authId = GetAuthUserId(ctx);
                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null || !me.CompanyId.HasValue)
                    return Results.Unauthorized();

                var ent = await repo.GetByIdAsync(id);
                if (ent == null || ent.CompanyId != me.CompanyId.Value)
                    return Results.NotFound();

                if (ent.Status.Equals("printed", StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest("Cannot edit a printed form.");

                ent.AccountHolderName = dto.AccountHolderName;
                ent.AuthorizedOnTheAccountName = dto.AuthorizedOnTheAccountName;
                ent.AccountNumber = dto.AccountNumber;

                // Service requests
                ent.ServiceRequests.ReactivateIdfaali = dto.ServiceRequests?.ReactivateIdfaali ?? ent.ServiceRequests.ReactivateIdfaali;
                ent.ServiceRequests.DeactivateIdfaali = dto.ServiceRequests?.DeactivateIdfaali ?? ent.ServiceRequests.DeactivateIdfaali;
                ent.ServiceRequests.ResetDigitalBankPassword = dto.ServiceRequests?.ResetDigitalBankPassword ?? ent.ServiceRequests.ResetDigitalBankPassword;
                ent.ServiceRequests.ResendMobileBankingPin = dto.ServiceRequests?.ResendMobileBankingPin ?? ent.ServiceRequests.ResendMobileBankingPin;
                ent.ServiceRequests.ChangePhoneNumber = dto.ServiceRequests?.ChangePhoneNumber ?? ent.ServiceRequests.ChangePhoneNumber;

                // Statement request (includes From/To dates)
                ent.StatementRequest.CurrentAccountStatementArabic = dto.StatementRequest?.CurrentAccountStatementArabic;
                ent.StatementRequest.CurrentAccountStatementEnglish = dto.StatementRequest?.CurrentAccountStatementEnglish;
                ent.StatementRequest.VisaAccountStatement = dto.StatementRequest?.VisaAccountStatement;
                ent.StatementRequest.AccountStatement = dto.StatementRequest?.AccountStatement;
                ent.StatementRequest.JournalMovement = dto.StatementRequest?.JournalMovement;
                ent.StatementRequest.NonFinancialCommitment = dto.StatementRequest?.NonFinancialCommitment;
                ent.StatementRequest.FromDate = dto.StatementRequest?.FromDate;
                ent.StatementRequest.ToDate = dto.StatementRequest?.ToDate;

                ent.OldAccountNumber = dto.OldAccountNumber;
                ent.NewAccountNumber = dto.NewAccountNumber;
                ent.TotalAmountLyd = dto.TotalAmountLyd;

                await repo.UpdateAsync(ent);
                log.LogInformation("Updated CertifiedBankStatementRequest Id={Id}", id);

                return Results.Ok(ToDto(ent));
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Auth error in UpdateCompanyRequest");
                return Results.Unauthorized();
            }
        }

        // ── ADMIN: list all ─────────────────────────────────────────────
        public static async Task<IResult> AdminGetAll(
            ICertifiedBankStatementRequestRepository repo,
            ILogger<CertifiedBankStatementRequestEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            var total = await repo.GetCountAsync(searchTerm, searchBy);
            var list = await repo.GetAllAsync(searchTerm, searchBy, page, limit);

            var dtos = list.Select(ToDto).ToList();

            return Results.Ok(new PagedResult<CertifiedBankStatementRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit)
            });
        }

        // ── ADMIN: update status & reason (with optional refund) ────────
        public static async Task<IResult> AdminUpdateStatus(
            int id,
            [FromBody] CertifiedBankStatementRequestStatusUpdateDto dto,
            ICertifiedBankStatementRequestRepository repo,
            ILogger<CertifiedBankStatementRequestEndpoints> log,
            [FromServices] CompGateApiDbContext db,
            [FromServices] IGenericTransferRepository genericTransferRepo)
        {
            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound();

            if (dto.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase) &&
                ent.TransferRequestId.HasValue)
            {
                // find original transfer for amount + accounts
                var tr = await db.TransferRequests.AsNoTracking()
                             .FirstOrDefaultAsync(t => t.Id == ent.TransferRequestId.Value);
                if (tr == null)
                    return Results.BadRequest("Refund failed: original transfer not found.");

                var originalRef = string.IsNullOrWhiteSpace(ent.BankReference)
                    ? tr.BankReference
                    : ent.BankReference;

                if (string.IsNullOrWhiteSpace(originalRef))
                    return Results.BadRequest("Refund failed: original bank reference missing.");

                var rv = await genericTransferRepo.RefundByOriginalRefAsync(
                    originalBankRef: originalRef!,
                    currencyCode: DEFAULT_CCY,
                    srcAcc: tr.ToAccount,   // fee account (received) → debit
                    dstAcc: tr.FromAccount, // customer account (sent) → credit
                    srcAcc2: tr.ToAccount,
                    dstAcc2: tr.FromAccount,
                    amount: tr.Amount,
                    note: $"Refund certified statement #{ent.Id}"
                );

                if (!rv.Success)
                    return Results.BadRequest("Refund failed: " + rv.Error);
            }

            ent.Status = dto.Status;
            ent.Reason = dto.Reason;
            await repo.UpdateAsync(ent);

            return Results.Ok(ToDto(ent));
        }

        // ── ADMIN: get by id ────────────────────────────────────────────
        public static async Task<IResult> AdminGetById(
            int id,
            [FromServices] ICertifiedBankStatementRequestRepository repo,
            [FromServices] ILogger<CertifiedBankStatementRequestEndpoints> log)
        {
            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound();

            return Results.Ok(ToDto(ent));
        }
    }
}
