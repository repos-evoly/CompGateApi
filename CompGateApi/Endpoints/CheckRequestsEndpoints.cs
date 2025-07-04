// CompGateApi.Endpoints/CheckRequestEndpoints.cs
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
    public class CheckRequestEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            // ── COMPANY PORTAL ─────────────────────────────────────────────
            var company = app
                .MapGroup("/api/checkrequests")
                .WithTags("CheckRequests")
                .RequireAuthorization("RequireCompanyUser");

            // List all company check-requests
            company.MapGet("/", GetCompanyRequests)
                   .WithName("GetCompanyCheckRequests")
                   .Produces<PagedResult<CheckRequestDto>>(200);

            // Get single by Id
            company.MapGet("/{id:int}", GetCompanyRequestById)
                   .WithName("GetCompanyCheckRequestById")
                   .Produces<CheckRequestDto>(200)
                   .Produces(404);

            // Create new
            company.MapPost("/", CreateCompanyRequest)
                   .WithName("CreateCheckRequest")
                   .Accepts<CheckRequestCreateDto>("application/json")
                   .Produces<CheckRequestDto>(201)
                   .Produces(400)
                   .Produces(401);

            // ── ADMIN PORTAL ───────────────────────────────────────────────
            var admin = app
                .MapGroup("/api/admin/checkrequests")
                .WithTags("CheckRequests")
                .RequireAuthorization("RequireAdminUser")
                .RequireAuthorization("AdminAccess");

            admin.MapGet("/", AdminGetAll)
                 .WithName("AdminGetAllCheckRequests")
                 .Produces<PagedResult<CheckRequestDto>>(200);

            admin.MapGet("/{id:int}", AdminGetById)
                 .WithName("AdminGetCheckRequestById")
                 .Produces<CheckRequestDto>(200)
                 .Produces(404);

            admin.MapPut("/{id:int}/status", AdminUpdateStatus)
                 .WithName("AdminUpdateCheckRequestStatus")
                 .Accepts<CheckRequestStatusUpdateDto>("application/json")
                 .Produces<CheckRequestDto>(200)
                 .Produces(400)
                 .Produces(404);
        }

        // helper to extract authenticated user
        private static int GetAuthUserId(HttpContext ctx)
        {
            var raw = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? ctx.User.FindFirst("nameid")?.Value;
            if (!int.TryParse(raw, out var id))
                throw new UnauthorizedAccessException(
                    $"Missing or invalid 'nameid' claim. Raw='{raw ?? "(null)"}'.");
            return id;
        }

        // ── COMPANY: list requests by company ───────────────────────────
        public static async Task<IResult> GetCompanyRequests(
            HttpContext ctx,
            ICheckRequestRepository repo,
            IUserRepository userRepo,
            ILogger<CheckRequestEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            try
            {
                var authId = GetAuthUserId(ctx);
                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null) return Results.Unauthorized();

                // fetch by company
                if (!me.CompanyId.HasValue)
                    return Results.Unauthorized();

                var cid = me.CompanyId.Value;
                var list = await repo.GetAllByCompanyAsync(
                    cid, searchTerm, searchBy, page, limit);
                var total = await repo.GetCountByCompanyAsync(
                    cid, searchTerm, searchBy);

                var dtos = list.Select(r => new CheckRequestDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    Branch = r.Branch,
                    BranchNum = r.BranchNum,
                    Date = r.Date,
                    CustomerName = r.CustomerName,
                    CardNum = r.CardNum,
                    AccountNum = r.AccountNum,
                    Beneficiary = r.Beneficiary,
                    Status = r.Status,
                    Reason = r.Reason,
                    LineItems = r.LineItems.Select(li => new CheckRequestLineItemDto
                    {
                        Id = li.Id,
                        Dirham = li.Dirham,
                        Lyd = li.Lyd
                    }).ToList(),
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                }).ToList();

                return Results.Ok(new PagedResult<CheckRequestDto>
                {
                    Data = dtos,
                    Page = page,
                    Limit = limit,
                    TotalPages = (int)Math.Ceiling(total / (double)limit),
                    TotalRecords = total
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Unauthorized in GetCompanyRequests");
                return Results.Unauthorized();
            }
        }

        // ── COMPANY: get single by id ─────────────────────────────────
        public static async Task<IResult> GetCompanyRequestById(
            int id,
            HttpContext ctx,
            ICheckRequestRepository repo,
            IUserRepository userRepo,
            ILogger<CheckRequestEndpoints> log)
        {
            try
            {
                var authId = GetAuthUserId(ctx);
                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null) return Results.Unauthorized();

                var ent = await repo.GetByIdAsync(id);
                if (ent == null || !me.CompanyId.HasValue || ent.CompanyId != me.CompanyId.Value)
                    return Results.NotFound("Check request not found.");

                var dto = new CheckRequestDto
                {
                    Id = ent.Id,
                    UserId = ent.UserId,
                    Branch = ent.Branch,
                    BranchNum = ent.BranchNum,
                    Date = ent.Date,
                    CustomerName = ent.CustomerName,
                    CardNum = ent.CardNum,
                    AccountNum = ent.AccountNum,
                    Beneficiary = ent.Beneficiary,
                    Status = ent.Status,
                    Reason = ent.Reason,
                    LineItems = ent.LineItems.Select(li => new CheckRequestLineItemDto
                    {
                        Id = li.Id,
                        Dirham = li.Dirham,
                        Lyd = li.Lyd
                    }).ToList(),
                    CreatedAt = ent.CreatedAt,
                    UpdatedAt = ent.UpdatedAt
                };
                return Results.Ok(dto);
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Unauthorized in GetCompanyRequestById");
                return Results.Unauthorized();
            }
        }

        // ── COMPANY: create new request ───────────────────────────────
        public static async Task<IResult> CreateCompanyRequest(
            [FromBody] CheckRequestCreateDto dto,
            HttpContext ctx,
            ICheckRequestRepository repo,
            IUserRepository userRepo,
            IValidator<CheckRequestCreateDto> validator,
            ILogger<CheckRequestEndpoints> log)
        {
            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));

            try
            {
                var authId = GetAuthUserId(ctx);
                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null) return Results.Unauthorized();

                if (!me.CompanyId.HasValue)
                    return Results.Unauthorized();

                var ent = new CheckRequest
                {
                    UserId = me.UserId,
                    CompanyId = me.CompanyId.Value,
                    Branch = dto.Branch,
                    BranchNum = dto.BranchNum,
                    Date = dto.Date,
                    CustomerName = dto.CustomerName,
                    CardNum = dto.CardNum,
                    AccountNum = dto.AccountNum,
                    Beneficiary = dto.Beneficiary,
                    Status = "Pending",
                    LineItems = dto.LineItems.Select(li => new CheckRequestLineItem
                    {
                        Dirham = li.Dirham,
                        Lyd = li.Lyd
                    }).ToList()
                };

                await repo.CreateAsync(ent);
                log.LogInformation("Created CheckRequest Id={Id}", ent.Id);

                var outDto = new CheckRequestDto
                {
                    Id = ent.Id,
                    UserId = ent.UserId,
                    Branch = ent.Branch,
                    BranchNum = ent.BranchNum,
                    Date = ent.Date,
                    CustomerName = ent.CustomerName,
                    CardNum = ent.CardNum,
                    AccountNum = ent.AccountNum,
                    Beneficiary = ent.Beneficiary,
                    Status = ent.Status,
                    LineItems = ent.LineItems.Select(li => new CheckRequestLineItemDto
                    {
                        Id = li.Id,
                        Dirham = li.Dirham,
                        Lyd = li.Lyd
                    }).ToList(),
                    CreatedAt = ent.CreatedAt,
                    UpdatedAt = ent.UpdatedAt
                };
                return Results.Created($"/api/checkrequests/{ent.Id}", outDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Unauthorized in CreateCompanyRequest");
                return Results.Unauthorized();
            }
        }

        // ── ADMIN: list all ───────────────────────────────────────────
        public static async Task<IResult> AdminGetAll(
            ICheckRequestRepository repo,
            ILogger<CheckRequestEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            var list = await repo.GetAllAsync(searchTerm, searchBy, page, limit);
            var total = await repo.GetCountAsync(searchTerm, searchBy);

            var dtos = list.Select(r => new CheckRequestDto
            {
                Id = r.Id,
                UserId = r.UserId,
                Branch = r.Branch,
                BranchNum = r.BranchNum,
                Date = r.Date,
                CustomerName = r.CustomerName,
                CardNum = r.CardNum,
                AccountNum = r.AccountNum,
                Beneficiary = r.Beneficiary,
                Status = r.Status,
                Reason = r.Reason,
                LineItems = r.LineItems.Select(li => new CheckRequestLineItemDto
                {
                    Id = li.Id,
                    Dirham = li.Dirham,
                    Lyd = li.Lyd
                }).ToList(),
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            return Results.Ok(new PagedResult<CheckRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalPages = (int)Math.Ceiling(total / (double)limit),
                TotalRecords = total
            });
        }

        // ── ADMIN: get by id ──────────────────────────────────────────
        public static async Task<IResult> AdminGetById(
            int id,
            [FromServices] ICheckRequestRepository repo)
        {
            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound("Check request not found.");

            var dto = new CheckRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                Branch = ent.Branch,
                BranchNum = ent.BranchNum,
                Date = ent.Date,
                CustomerName = ent.CustomerName,
                CardNum = ent.CardNum,
                AccountNum = ent.AccountNum,
                Beneficiary = ent.Beneficiary,
                Status = ent.Status,
                Reason = ent.Reason,
                LineItems = ent.LineItems.Select(li => new CheckRequestLineItemDto
                {
                    Id = li.Id,
                    Dirham = li.Dirham,
                    Lyd = li.Lyd
                }).ToList(),
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Ok(dto);
        }

        // ── ADMIN: update status & audit ─────────────────────────────
        public static async Task<IResult> AdminUpdateStatus(
            int id,
            [FromBody] CheckRequestStatusUpdateDto dto,
            [FromServices] ICheckRequestRepository repo,
            [FromServices] IValidator<CheckRequestStatusUpdateDto> validator,
            // [FromServices] IAuditLogRepository auditRepo,
            HttpContext ctx)
        {
            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));

            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound("Check request not found.");

            ent.Status = dto.Status;
            await repo.UpdateAsync(ent);

            var adminId = GetAuthUserId(ctx);
            // await auditRepo.CreateAsync(new AuditLog
            // {
            //     UserId = adminId,
            //     Action = $"Updated CheckRequest {id} status to '{dto.Status}'"
            // });

            var dtoOut = new CheckRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                Branch = ent.Branch,
                BranchNum = ent.BranchNum,
                Date = ent.Date,
                CustomerName = ent.CustomerName,
                CardNum = ent.CardNum,
                AccountNum = ent.AccountNum,
                Beneficiary = ent.Beneficiary,
                Status = ent.Status,
                Reason = ent.Reason,
                LineItems = ent.LineItems.Select(li => new CheckRequestLineItemDto
                {
                    Id = li.Id,
                    Dirham = li.Dirham,
                    Lyd = li.Lyd
                }).ToList(),
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Ok(dtoOut);
        }
    }
}
