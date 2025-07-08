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
using CompGateApi.Data.Models;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        static int GetAuthUserId(HttpContext ctx)
        {
            var raw = ctx.User.FindFirst("nameid")?.Value
                   ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(raw, out var id)) return id;
            throw new UnauthorizedAccessException("Missing/invalid 'nameid' claim.");
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

            var dtos = list.Select(r => new CertifiedBankStatementRequestDto
            {
                Id = r.Id,
                CompanyId = r.CompanyId,
                AccountHolderName = r.AccountHolderName,
                AuthorizedOnTheAccountName = r.AuthorizedOnTheAccountName,
                AccountNumber = r.AccountNumber,
                // flatten serviceRequests/statementRequest into your DTO as needed…
                Status = r.Status,
                Reason = r.Reason,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

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

            return Results.Ok(new CertifiedBankStatementRequestDto
            {
                Id = ent.Id,
                CompanyId = ent.CompanyId,
                AccountHolderName = ent.AccountHolderName,
                AuthorizedOnTheAccountName = ent.AuthorizedOnTheAccountName,
                AccountNumber = ent.AccountNumber,
                OldAccountNumber = ent.OldAccountNumber,
                NewAccountNumber = ent.NewAccountNumber,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            });
        }

        // ── COMPANY: create ─────────────────────────────────────────────
        public static async Task<IResult> CreateCompanyRequest(
            [FromBody] CertifiedBankStatementRequestCreateDto dto,
            HttpContext ctx,
            ICertifiedBankStatementRequestRepository repo,
            IUserRepository userRepo,
            IValidator<CertifiedBankStatementRequestCreateDto> validator,
            ILogger<CertifiedBankStatementRequestEndpoints> log)
        {
            // var v = await validator.ValidateAsync(dto);
            // if (!v.IsValid)
            //     return Results.BadRequest(v.Errors.Select(e => e.ErrorMessage));

            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

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
                Status = "Pending",
                Reason = string.Empty
            };

            await repo.CreateAsync(ent);
            log.LogInformation("Created CertifiedBankStatementRequest Id={Id}", ent.Id);

            var outDto = new CertifiedBankStatementRequestDto
            {
                Id = ent.Id,
                CompanyId = ent.CompanyId,
                AccountHolderName = ent.AccountHolderName,
                AuthorizedOnTheAccountName = ent.AuthorizedOnTheAccountName,
                AccountNumber = ent.AccountNumber,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Created($"/api/certifiedbankstatementrequests/{ent.Id}", outDto);
        }

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

                // update fields
                ent.AccountHolderName = dto.AccountHolderName;
                ent.AuthorizedOnTheAccountName = dto.AuthorizedOnTheAccountName;
                ent.AccountNumber = dto.AccountNumber;

                ent.ServiceRequests.ReactivateIdfaali = dto.ServiceRequests?.ReactivateIdfaali ?? ent.ServiceRequests.ReactivateIdfaali;
                ent.ServiceRequests.DeactivateIdfaali = dto.ServiceRequests?.DeactivateIdfaali ?? ent.ServiceRequests.DeactivateIdfaali;
                ent.ServiceRequests.ResetDigitalBankPassword = dto.ServiceRequests?.ResetDigitalBankPassword ?? ent.ServiceRequests.ResetDigitalBankPassword;
                ent.ServiceRequests.ResendMobileBankingPin = dto.ServiceRequests?.ResendMobileBankingPin ?? ent.ServiceRequests.ResendMobileBankingPin;
                ent.ServiceRequests.ChangePhoneNumber = dto.ServiceRequests?.ChangePhoneNumber ?? ent.ServiceRequests.ChangePhoneNumber;

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

                await repo.UpdateAsync(ent);
                log.LogInformation("Updated CertifiedBankStatementRequest Id={Id}", id);

                var outDto = new CertifiedBankStatementRequestDto
                {
                    Id = ent.Id,
                    CompanyId = ent.CompanyId,
                    AccountHolderName = ent.AccountHolderName,
                    AuthorizedOnTheAccountName = ent.AuthorizedOnTheAccountName,
                    AccountNumber = ent.AccountNumber,
                    Status = ent.Status,
                    Reason = ent.Reason,
                    CreatedAt = ent.CreatedAt,
                    UpdatedAt = ent.UpdatedAt
                };

                return Results.Ok(outDto);
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

            var dtos = list.Select(r => new CertifiedBankStatementRequestDto
            {
                Id = r.Id,
                CompanyId = r.CompanyId,
                AccountHolderName = r.AccountHolderName,
                AuthorizedOnTheAccountName = r.AuthorizedOnTheAccountName,
                AccountNumber = r.AccountNumber,
                Status = r.Status,
                Reason = r.Reason,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            return Results.Ok(new PagedResult<CertifiedBankStatementRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit)
            });
        }

        // ── ADMIN: update status & reason ─────────────────────────────
        public static async Task<IResult> AdminUpdateStatus(
            int id,
            [FromBody] CertifiedBankStatementRequestStatusUpdateDto dto,
            ICertifiedBankStatementRequestRepository repo,
            // IValidator<CertifiedBankStatementRequestStatusUpdateDto> validator,
            ILogger<CertifiedBankStatementRequestEndpoints> log)
        {
            // var v = await validator.ValidateAsync(dto);
            // if (!v.IsValid)
            //     return Results.BadRequest(v.Errors.Select(e => e.ErrorMessage));

            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound();

            ent.Status = dto.Status;
            ent.Reason = dto.Reason;
            await repo.UpdateAsync(ent);

            return Results.Ok(new CertifiedBankStatementRequestDto
            {
                Id = ent.Id,
                CompanyId = ent.CompanyId,
                AccountHolderName = ent.AccountHolderName,
                AuthorizedOnTheAccountName = ent.AuthorizedOnTheAccountName,
                AccountNumber = ent.AccountNumber,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            });
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

            return Results.Ok(new CertifiedBankStatementRequestDto
            {
                Id = ent.Id,
                CompanyId = ent.CompanyId,
                AccountHolderName = ent.AccountHolderName,
                AuthorizedOnTheAccountName = ent.AuthorizedOnTheAccountName,
                AccountNumber = ent.AccountNumber,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            });
        }
    }
}
