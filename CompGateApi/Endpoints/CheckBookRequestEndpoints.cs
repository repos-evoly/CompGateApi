// CompGateApi.Endpoints/CheckBookRequestEndpoints.cs
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
    public class CheckBookRequestEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            // ── COMPANY ROUTES ────────────────────────────────────────────────
            var company = app
                .MapGroup("/api/checkbookrequests")
                .WithTags("CheckBookRequests");

            // Require authentication + company roles
            company.RequireAuthorization("RequireCompanyUser")
                   .RequireAuthorization("CanRequestCheckBook");

            company.MapGet("/", GetMyRequests)
                   .WithName("GetMyCheckBookRequests")
                   .Produces<PagedResult<CheckBookRequestDto>>(200);

            company.MapGet("/{id:int}", GetMyRequestById)
                   .WithName("GetMyCheckBookRequestById")
                   .Produces<CheckBookRequestDto>(200)
                   .Produces(404);

            company.MapPost("/", CreateMyRequest)
                   .WithName("CreateCheckBookRequest")
                   .Accepts<CheckBookRequestCreateDto>("application/json")
                   .Produces<CheckBookRequestDto>(201)
                   .Produces(400);

            // ── ADMIN ROUTES ──────────────────────────────────────────────────
            var admin = app
                .MapGroup("/api/admin/checkbookrequests")
                .WithTags("CheckBookRequests");

            admin.RequireAuthorization("RequireAdminUser")
                 .RequireAuthorization("AdminAccess");

            admin.MapGet("/", GetAllAdmin)
                 .WithName("AdminGetCheckBookRequests")
                 .Produces<PagedResult<CheckBookRequestDto>>(200);

            admin.MapPut("/{id:int}/status", UpdateStatus)
                 .WithName("AdminUpdateCheckBookRequestStatus")
                 .Accepts<CheckBookRequestStatusUpdateDto>("application/json")
                 .Produces<CheckBookRequestDto>(200)
                 .Produces(404);
            admin.MapGet("/{id:int}", AdminGetById)
                .WithName("AdminGetCheckBookRequestById")
                .Produces<CheckBookRequestDto>(200)
                .Produces(404);

        }

        /// <summary>
        /// Extracts the raw "nameid" claim from the JWT (not the mapped ClaimTypes.NameIdentifier).
        /// Throws if missing or non-int.
        /// </summary>
        private static int GetAuthUserId(HttpContext ctx)
        {
            // YOUR TOKEN _contains_ a claim named "nameid" (per your NameClaimType setting).
            var raw = ctx.User.FindFirst("nameid")?.Value;
            if (int.TryParse(raw, out var id))
                return id;

            throw new UnauthorizedAccessException(
                $"Missing or invalid 'nameid' claim. Raw value='{raw ?? "(null)"}'.");
        }

        // ── Company: list own requests ─────────────────────────────────────
        public static async Task<IResult> GetMyRequests(
            HttpContext ctx,
            ICheckBookRequestRepository repo,
            IUserRepository userRepo,
            ILogger<CheckBookRequestEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            // Log identity + all claims so you can inspect what's coming through
            log.LogInformation("GetMyRequests: IsAuthenticated={IsAuth}",
                ctx.User.Identity?.IsAuthenticated == true);
            log.LogInformation("Incoming Claims:\n  {Claims}",
                string.Join("\n  ", ctx.User.Claims.Select(c => $"{c.Type} = {c.Value}")));

            try
            {
                var authId = GetAuthUserId(ctx);
                log.LogDebug("Parsed AuthUserId = {AuthId}", authId);

                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null)
                {
                    log.LogWarning("No local user mapping for AuthId={AuthId}", authId);
                    return Results.Unauthorized();
                }

                if (!me.CompanyId.HasValue)
                    return Results.Unauthorized();

                var list = await repo.GetAllByCompanyAsync(me.CompanyId.Value, searchTerm, searchBy, page, limit);
                var total = await repo.GetCountByCompanyAsync(me.CompanyId.Value, searchTerm, searchBy);

                var dtos = list.Select(r => new CheckBookRequestDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    FullName = r.FullName,
                    Address = r.Address,
                    AccountNumber = r.AccountNumber,
                    PleaseSend = r.PleaseSend,
                    Branch = r.Branch,
                    Date = r.Date,
                    BookContaining = r.BookContaining,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                }).ToList();

                log.LogInformation("Returning {Count}/{Total} records for UserId={UserId}",
                    dtos.Count, total, me.UserId);

                return Results.Ok(new PagedResult<CheckBookRequestDto>
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
                log.LogError(ex, "Authorization error in GetMyRequests");
                return Results.Unauthorized();
            }
        }

        // ── Company: get single request by ID ─────────────────────────────
        public static async Task<IResult> GetMyRequestById(
            int id,
            HttpContext ctx,
            ICheckBookRequestRepository repo,
            IUserRepository userRepo,
            ILogger<CheckBookRequestEndpoints> log)
        {
            log.LogInformation("GetMyRequestById({Id})", id);
            try
            {
                var authId = GetAuthUserId(ctx);
                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null)
                {
                    log.LogWarning("Unauthorized: no local user for AuthId={AuthId}", authId);
                    return Results.Unauthorized();
                }

                var ent = await repo.GetByIdAsync(id);
                if (ent == null
                    || !me.CompanyId.HasValue
                    || ent.CompanyId != me.CompanyId.Value)
                    return Results.NotFound("Not found");


                return Results.Ok(new CheckBookRequestDto
                {
                    Id = ent.Id,
                    UserId = ent.UserId,
                    FullName = ent.FullName,
                    Address = ent.Address,
                    AccountNumber = ent.AccountNumber,
                    PleaseSend = ent.PleaseSend,
                    Branch = ent.Branch,
                    Date = ent.Date,
                    BookContaining = ent.BookContaining,
                    Status = ent.Status,
                    CreatedAt = ent.CreatedAt,
                    UpdatedAt = ent.UpdatedAt
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Auth error in GetMyRequestById");
                return Results.Unauthorized();
            }
        }

        // ── Company: create a new request ─────────────────────────────────
        public static async Task<IResult> CreateMyRequest(
            [FromBody] CheckBookRequestCreateDto dto,
            HttpContext ctx,
            ICheckBookRequestRepository repo,
            IUserRepository userRepo,
            IValidator<CheckBookRequestCreateDto> validator,
            ILogger<CheckBookRequestEndpoints> log)
        {
            log.LogInformation("CreateMyRequest called. Payload={@Dto}", dto);

            // Validate payload
            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                log.LogWarning("Validation errors: {Errors}",
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));
            }

            try
            {
                // Resolve and log user
                var authId = GetAuthUserId(ctx);
                log.LogDebug("Parsed AuthUserId = {AuthId}", authId);

                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null)
                {
                    log.LogWarning("No local user mapping for AuthId={AuthId}", authId);
                    return Results.Unauthorized();
                }
                if (!me.CompanyId.HasValue)
                    return Results.Unauthorized();



                // Build & save entity
                var ent = new CheckBookRequest
                {
                    UserId = me.UserId,
                    CompanyId = me.CompanyId.Value,
                    FullName = dto.FullName,
                    Address = dto.Address,
                    AccountNumber = dto.AccountNumber,
                    PleaseSend = dto.PleaseSend,
                    Branch = dto.Branch,
                    Date = dto.Date,
                    BookContaining = dto.BookContaining,
                    Status = "Pending"
                };
                await repo.CreateAsync(ent);
                log.LogInformation("Persisted new CheckBookRequest Id={RequestId}", ent.Id);

                // Return DTO
                var outDto = new CheckBookRequestDto
                {
                    Id = ent.Id,
                    UserId = ent.UserId,
                    CompanyId = me.CompanyId.Value,
                    FullName = ent.FullName,
                    Address = ent.Address,
                    AccountNumber = ent.AccountNumber,
                    PleaseSend = ent.PleaseSend,
                    Branch = ent.Branch,
                    Date = ent.Date,
                    BookContaining = ent.BookContaining,
                    Status = ent.Status,
                    CreatedAt = ent.CreatedAt,
                    UpdatedAt = ent.UpdatedAt
                };
                return Results.Created($"/api/checkbookrequests/{ent.Id}", outDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Auth error in CreateMyRequest");
                return Results.Unauthorized();
            }
        }

        // ── Admin: list all ────────────────────────────────────────────────
        public static async Task<IResult> GetAllAdmin(
            ICheckBookRequestRepository repo,
            ILogger<CheckBookRequestEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            log.LogInformation("Admin:GetAll called (search='{Search}', by='{By}', page={Page}, limit={Limit})",
                searchTerm, searchBy, page, limit);

            var list = await repo.GetAllAsync(searchTerm, searchBy, page, limit);
            var total = await repo.GetCountAsync(searchTerm, searchBy);

            var dtos = list.Select(r => new CheckBookRequestDto
            {
                Id = r.Id,
                UserId = r.UserId,
                FullName = r.FullName,
                Address = r.Address,
                AccountNumber = r.AccountNumber,
                PleaseSend = r.PleaseSend,
                Branch = r.Branch,
                Date = r.Date,
                BookContaining = r.BookContaining,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            log.LogInformation("Admin fetched {Count}/{Total} requests", dtos.Count, total);

            return Results.Ok(new PagedResult<CheckBookRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalPages = (int)Math.Ceiling(total / (double)limit),
                TotalRecords = total
            });
        }

        // ── Admin: update status ───────────────────────────────────────────
        public static async Task<IResult> UpdateStatus(
            int id,
            CheckBookRequestStatusUpdateDto dto,
            ICheckBookRequestRepository repo,
            IValidator<CheckBookRequestStatusUpdateDto> validator,
            ILogger<CheckBookRequestEndpoints> log)
        {
            log.LogInformation("Admin:UpdateStatus({Id}) → Status='{Status}'", id, dto.Status);

            var res = await validator.ValidateAsync(dto);
            if (!res.IsValid)
            {
                log.LogWarning("Validation errors: {Errors}",
                    string.Join("; ", res.Errors.Select(e => e.ErrorMessage)));
                return Results.BadRequest(res.Errors.Select(e => e.ErrorMessage));
            }

            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
            {
                log.LogWarning("CheckBookRequest {Id} not found", id);
                return Results.NotFound("Not found");
            }

            ent.Status = dto.Status;
            await repo.UpdateAsync(ent);
            log.LogInformation("Updated CheckBookRequest {Id} → new Status='{Status}'", id, dto.Status);

            return Results.Ok(new CheckBookRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                FullName = ent.FullName,
                Address = ent.Address,
                AccountNumber = ent.AccountNumber,
                PleaseSend = ent.PleaseSend,
                Branch = ent.Branch,
                Date = ent.Date,
                BookContaining = ent.BookContaining,
                Status = ent.Status,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            });
        }
        public static async Task<IResult> AdminGetById(
    int id,
    [FromServices] ICheckBookRequestRepository repo,
    [FromServices] ILogger<CheckBookRequestEndpoints> log)
        {
            log.LogInformation("AdminGetById({Id})", id);
            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
            {
                log.LogWarning("CheckBookRequest {Id} not found", id);
                return Results.NotFound("Check‐book request not found.");
            }

            var dto = new CheckBookRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                FullName = ent.FullName,
                Address = ent.Address,
                AccountNumber = ent.AccountNumber,
                PleaseSend = ent.PleaseSend,
                Branch = ent.Branch,
                Date = ent.Date,
                BookContaining = ent.BookContaining,
                Status = ent.Status,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };

            return Results.Ok(dto);
        }

    }
}
