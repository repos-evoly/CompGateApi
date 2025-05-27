// CompGateApi.Endpoints/RtgsRequestEndpoints.cs
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
    public class RtgsRequestEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            // ── COMPANY routes ───────────────────────────────────────
            var company = app
                .MapGroup("/api/rtgsrequests")
                .WithTags("RtgsRequests")
                .RequireAuthorization("RequireCompanyUser")
                .RequireAuthorization("CanRequestRtgs");

            company.MapGet("/", GetMyRequests)
                   .Produces<PagedResult<RtgsRequestDto>>(200);

            company.MapGet("/{id:int}", GetMyRequestById)
                   .Produces<RtgsRequestDto>(200)
                   .Produces(404);

            company.MapPost("/", CreateMyRequest)
                   .Accepts<RtgsRequestCreateDto>("application/json")
                   .Produces<RtgsRequestDto>(201)
                   .Produces(400);

            // ── ADMIN routes ─────────────────────────────────────────
            var admin = app
                .MapGroup("/api/admin/rtgsrequests")
                .WithTags("RtgsRequests")
                .RequireAuthorization("RequireAdminUser")
                .RequireAuthorization("AdminAccess");

            admin.MapGet("/", GetAllAdmin)
                 .Produces<PagedResult<RtgsRequestDto>>(200);

            admin.MapPut("/{id:int}/status", UpdateStatus)
                 .Accepts<RtgsRequestStatusUpdateDto>("application/json")
                 .Produces<RtgsRequestDto>(200)
                 .Produces(404);

            admin.MapGet("/{id:int}", GetByIdAdmin)
                .WithName("AdminGetRtgsRequestById")
                .Produces<RtgsRequestDto>(200)
                .Produces(404);

        }

        private static int GetAuthUserId(HttpContext ctx)
        {
            var raw = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? ctx.User.FindFirst("nameid")?.Value;
            if (int.TryParse(raw, out var id)) return id;
            throw new UnauthorizedAccessException("Missing or invalid 'nameid' claim.");
        }

        // ── COMPANY: list own RTGS requests with search & paging ────────────────────────────
        public static async Task<IResult> GetMyRequests(
            HttpContext ctx,
            IRtgsRequestRepository repo,
            IUserRepository userRepo,
            ILogger<RtgsRequestEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            log.LogInformation("GetMyRequests called. Claims:\n{Claims}",
                string.Join("\n", ctx.User.Claims.Select(c => $"{c.Type}={c.Value}")));

            // authenticate user
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null) return Results.Unauthorized();

            if (!me.CompanyId.HasValue)
                return Results.Unauthorized();

            // fetch filtered & paged data
            var list = await repo.GetAllByCompanyAsync(me.CompanyId.Value, searchTerm, searchBy, page, limit);
            var total = await repo.GetCountByCompanyAsync(me.CompanyId.Value, searchTerm, searchBy);

            var dtos = list.Select(r => new RtgsRequestDto
            {
                Id = r.Id,
                UserId = r.UserId,
                RefNum = r.RefNum,
                Date = r.Date,
                PaymentType = r.PaymentType,
                AccountNo = r.AccountNo,
                ApplicantName = r.ApplicantName,
                Address = r.Address,
                BeneficiaryName = r.BeneficiaryName,
                BeneficiaryAccountNo = r.BeneficiaryAccountNo,
                BeneficiaryBank = r.BeneficiaryBank,
                BranchName = r.BranchName,
                Amount = r.Amount,
                RemittanceInfo = r.RemittanceInfo,
                Invoice = r.Invoice,
                Contract = r.Contract,
                Claim = r.Claim,
                OtherDoc = r.OtherDoc,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            return Results.Ok(new PagedResult<RtgsRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit)
            });
        }

        // ── COMPANY: get single RTGS request ──────────────────────────
        public static async Task<IResult> GetMyRequestById(
            int id,
            HttpContext ctx,
            IRtgsRequestRepository repo,
            IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null) return Results.Unauthorized();

            var ent = await repo.GetByIdAsync(id);
            if (ent == null || !me.CompanyId.HasValue || ent.CompanyId != me.CompanyId.Value)
                return Results.NotFound();

            var dto = new RtgsRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                RefNum = ent.RefNum,
                Date = ent.Date,
                PaymentType = ent.PaymentType,
                AccountNo = ent.AccountNo,
                ApplicantName = ent.ApplicantName,
                Address = ent.Address,
                BeneficiaryName = ent.BeneficiaryName,
                BeneficiaryAccountNo = ent.BeneficiaryAccountNo,
                BeneficiaryBank = ent.BeneficiaryBank,
                BranchName = ent.BranchName,
                Amount = ent.Amount,
                RemittanceInfo = ent.RemittanceInfo,
                Invoice = ent.Invoice,
                Contract = ent.Contract,
                Claim = ent.Claim,
                OtherDoc = ent.OtherDoc,
                Status = ent.Status,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Ok(dto);
        }

        // ── COMPANY: create new RTGS request ─────────────────────────────
        public static async Task<IResult> CreateMyRequest(
            [FromBody] RtgsRequestCreateDto dto,
            HttpContext ctx,
            IRtgsRequestRepository repo,
            IUserRepository userRepo,
            IValidator<RtgsRequestCreateDto> validator,
            ILogger<RtgsRequestEndpoints> log)
        {
            log.LogInformation("CreateMyRequest payload: {@Dto}", dto);
            var res = await validator.ValidateAsync(dto);
            if (!res.IsValid)
                return Results.BadRequest(res.Errors.Select(e => e.ErrorMessage));

            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null) return Results.Unauthorized();

            if (!me.CompanyId.HasValue)
                return Results.Unauthorized();

            var ent = new RtgsRequest
            {
                UserId = me.UserId,
                RefNum = dto.RefNum,
                CompanyId = me.CompanyId.Value,
                Date = dto.Date,
                PaymentType = dto.PaymentType,
                AccountNo = dto.AccountNo,
                ApplicantName = dto.ApplicantName,
                Address = dto.Address,
                BeneficiaryName = dto.BeneficiaryName,
                BeneficiaryAccountNo = dto.BeneficiaryAccountNo,
                BeneficiaryBank = dto.BeneficiaryBank,
                BranchName = dto.BranchName,
                Amount = dto.Amount,
                RemittanceInfo = dto.RemittanceInfo,
                Invoice = dto.Invoice,
                Contract = dto.Contract,
                Claim = dto.Claim,
                OtherDoc = dto.OtherDoc,
                Status = "Pending"
            };

            await repo.CreateAsync(ent);
            log.LogInformation("Created RtgsRequest Id={Id}", ent.Id);

            var outDto = new RtgsRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                RefNum = ent.RefNum,
                Date = ent.Date,
                PaymentType = ent.PaymentType,
                AccountNo = ent.AccountNo,
                ApplicantName = ent.ApplicantName,
                Address = ent.Address,
                BeneficiaryName = ent.BeneficiaryName,
                BeneficiaryAccountNo = ent.BeneficiaryAccountNo,
                BeneficiaryBank = ent.BeneficiaryBank,
                BranchName = ent.BranchName,
                Amount = ent.Amount,
                RemittanceInfo = ent.RemittanceInfo,
                Invoice = ent.Invoice,
                Contract = ent.Contract,
                Claim = ent.Claim,
                OtherDoc = ent.OtherDoc,
                Status = ent.Status,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };

            return Results.Created($"/api/rtgsrequests/{ent.Id}", outDto);
        }

        // ── ADMIN: list all RTGS requests with search & paging ──────────────────────────────
        public static async Task<IResult> GetAllAdmin(
            IRtgsRequestRepository repo,
            ILogger<RtgsRequestEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            log.LogInformation("Admin:GetAll called (search={Search},by={By},page={Page},limit={Limit})",
                searchTerm, searchBy, page, limit);

            var list = await repo.GetAllAsync(searchTerm, searchBy, page, limit);
            var total = await repo.GetCountAsync(searchTerm, searchBy);

            var dtos = list.Select(r => new RtgsRequestDto
            {
                Id = r.Id,
                UserId = r.UserId,
                RefNum = r.RefNum,
                Date = r.Date,
                PaymentType = r.PaymentType,
                AccountNo = r.AccountNo,
                ApplicantName = r.ApplicantName,
                Address = r.Address,
                BeneficiaryName = r.BeneficiaryName,
                BeneficiaryAccountNo = r.BeneficiaryAccountNo,
                BeneficiaryBank = r.BeneficiaryBank,
                BranchName = r.BranchName,
                Amount = r.Amount,
                RemittanceInfo = r.RemittanceInfo,
                Invoice = r.Invoice,
                Contract = r.Contract,
                Claim = r.Claim,
                OtherDoc = r.OtherDoc,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            return Results.Ok(new PagedResult<RtgsRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit)
            });
        }

        // ── ADMIN: update status ─────────────────────────
        public static async Task<IResult> UpdateStatus(
            int id,
            [FromBody] RtgsRequestStatusUpdateDto dto,
            IRtgsRequestRepository repo,
            IValidator<RtgsRequestStatusUpdateDto> validator,
            ILogger<RtgsRequestEndpoints> log)
        {
            var res = await validator.ValidateAsync(dto);
            if (!res.IsValid)
                return Results.BadRequest(res.Errors.Select(e => e.ErrorMessage));

            var ent = await repo.GetByIdAsync(id);
            if (ent == null) return Results.NotFound();

            ent.Status = dto.Status;
            await repo.UpdateAsync(ent);

            var outDto = new RtgsRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                RefNum = ent.RefNum,
                Date = ent.Date,
                PaymentType = ent.PaymentType,
                AccountNo = ent.AccountNo,
                ApplicantName = ent.ApplicantName,
                Address = ent.Address,
                BeneficiaryName = ent.BeneficiaryName,
                BeneficiaryAccountNo = ent.BeneficiaryAccountNo,
                BeneficiaryBank = ent.BeneficiaryBank,
                BranchName = ent.BranchName,
                Amount = ent.Amount,
                RemittanceInfo = ent.RemittanceInfo,
                Invoice = ent.Invoice,
                Contract = ent.Contract,
                Claim = ent.Claim,
                OtherDoc = ent.OtherDoc,
                Status = ent.Status,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Ok(outDto);
        }

        public static async Task<IResult> GetByIdAdmin(
    int id,
    [FromServices] IRtgsRequestRepository repo,
    [FromServices] ILogger<RtgsRequestEndpoints> log)
        {
            log.LogInformation("Admin:GetByIdAdmin({Id})", id);

            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound("RTGS request not found.");

            var dto = new RtgsRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                RefNum = ent.RefNum,
                Date = ent.Date,
                PaymentType = ent.PaymentType,
                AccountNo = ent.AccountNo,
                ApplicantName = ent.ApplicantName,
                Address = ent.Address,
                BeneficiaryName = ent.BeneficiaryName,
                BeneficiaryAccountNo = ent.BeneficiaryAccountNo,
                BeneficiaryBank = ent.BeneficiaryBank,
                BranchName = ent.BranchName,
                Amount = ent.Amount,
                RemittanceInfo = ent.RemittanceInfo,
                Invoice = ent.Invoice,
                Contract = ent.Contract,
                Claim = ent.Claim,
                OtherDoc = ent.OtherDoc,
                Status = ent.Status,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Ok(dto);
        }
    }
}
