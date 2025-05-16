// CompGateApi.Endpoints/CheckRequestEndpoints.cs
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
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
              

            company.MapGet("/", GetMyRequests)
                   .WithName("GetMyCheckRequests")
                   .Produces<PagedResult<CheckRequestDto>>(200);

            company.MapGet("/{id:int}", GetMyRequestById)
                   .WithName("GetMyCheckRequestById")
                   .Produces<CheckRequestDto>(200)
                   .Produces(404);

            company.MapPost("/", CreateMyRequest)
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

            admin.MapGet("/", GetAll)
                 .WithName("AdminGetAllCheckRequests")
                 .Produces<PagedResult<CheckRequestDto>>(200);

            admin.MapPut("/{id:int}/status", UpdateStatus)
                 .WithName("AdminUpdateCheckRequestStatus")
                 .Accepts<CheckRequestStatusUpdateDto>("application/json")
                 .Produces<CheckRequestDto>(200)
                 .Produces(400)
                 .Produces(404);
        }

        /// <summary>
        /// Extracts the raw "nameid" claim from the JWT. Throws if missing or invalid.
        /// </summary>
        private static int GetAuthUserId(HttpContext ctx)
        {
            var raw = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? ctx.User.FindFirst("nameid")?.Value;
            if (int.TryParse(raw, out var id))
                return id;

            throw new UnauthorizedAccessException(
                $"Missing or invalid 'nameid' claim. Raw='{raw ?? "(null)"}'.");
        }

        // ── COMPANY: list own check-requests ────────────────────────────
        public static async Task<IResult> GetMyRequests(
            HttpContext ctx,
            ICheckRequestRepository repo,
            IUserRepository userRepo,
            ILogger<CheckRequestEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            log.LogInformation("GetMyRequests called. Authenticated={IsAuth}",
                ctx.User.Identity?.IsAuthenticated == true);

            try
            {
                var authId = GetAuthUserId(ctx);
                log.LogDebug("Parsed AuthUserId = {AuthId}", authId);

                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null) return Results.Unauthorized();

                var list = await repo.GetAllByUserAsync(me.UserId, searchTerm, searchBy, page, limit);
                var total = await repo.GetCountByUserAsync(me.UserId, searchTerm, searchBy);

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
                log.LogError(ex, "Auth error in GetMyRequests");
                return Results.Unauthorized();
            }
        }

        // ── COMPANY: get single request by ID ──────────────────────────
        public static async Task<IResult> GetMyRequestById(
            int id,
            HttpContext ctx,
            ICheckRequestRepository repo,
            IUserRepository userRepo,
            ILogger<CheckRequestEndpoints> log)
        {
            log.LogInformation("GetMyRequestById({Id})", id);
            try
            {
                var authId = GetAuthUserId(ctx);
                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                var ent = await repo.GetByIdAsync(id);

                if (me == null || ent == null || ent.UserId != me.UserId)
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
                log.LogError(ex, "Auth error in GetMyRequestById");
                return Results.Unauthorized();
            }
        }

        // ── COMPANY: create a new request ─────────────────────────────
        public static async Task<IResult> CreateMyRequest(
            [FromBody] CheckRequestCreateDto dto,
            HttpContext ctx,
            ICheckRequestRepository repo,
            IUserRepository userRepo,
            IValidator<CheckRequestCreateDto> validator,
            ILogger<CheckRequestEndpoints> log)
        {
            log.LogInformation("CreateMyRequest called. Payload={@Dto}", dto);

            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                log.LogWarning("Validation errors: {Errors}",
                    validation.Errors.Select(e => e.ErrorMessage));
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));
            }

            try
            {
                var authId = GetAuthUserId(ctx);
                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null) return Results.Unauthorized();

                var ent = new CheckRequest
                {
                    UserId = me.UserId,
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
                log.LogError(ex, "Auth error in CreateMyRequest");
                return Results.Unauthorized();
            }
        }

        // ── ADMIN: list all check-requests ──────────────────────────────
        public static async Task<IResult> GetAll(
            ICheckRequestRepository repo,
            ILogger<CheckRequestEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            log.LogInformation("Admin:GetAll called (search={Search},by={By},page={Page},limit={Limit})",
                searchTerm, searchBy, page, limit);

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

        // ── ADMIN: update request status & audit ─────────────────────────
        public static async Task<IResult> UpdateStatus(
            int id,
            [FromBody] CheckRequestStatusUpdateDto dto,
            ICheckRequestRepository repo,
            IValidator<CheckRequestStatusUpdateDto> validator,
            IAuditLogRepository auditRepo,
            HttpContext ctx,
            ILogger<CheckRequestEndpoints> log)
        {
            log.LogInformation("Admin:UpdateStatus({Id}) → {Status}", id, dto.Status);

            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                log.LogWarning("Validation errors: {Errors}",
                    validation.Errors.Select(e => e.ErrorMessage));
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));
            }

            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound("Check request not found.");

            ent.Status = dto.Status;
            await repo.UpdateAsync(ent);
            log.LogInformation("Updated CheckRequest {Id} to Status={Status}", id, dto.Status);

            var adminId = GetAuthUserId(ctx);
            await auditRepo.CreateAsync(new AuditLog
            {
                UserId = adminId,
                Action = $"Updated CheckRequest {id} status to '{dto.Status}'"
            });

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
            return Results.Ok(outDto);
        }
    }
}
