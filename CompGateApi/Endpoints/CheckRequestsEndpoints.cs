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

            company.MapPut("/{id:int}", UpdateCompanyRequest)
                    .WithName("UpdateCheckRequest")
                    .Accepts<CheckRequestCreateDto>("application/json")
                    .Produces<CheckRequestDto>(200)
                    .Produces(400)
                    .Produces(404);


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
     IRepresentativeRepository repRepo,
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
                if (me == null || !me.CompanyId.HasValue)
                    return Results.Unauthorized();

                var cid = me.CompanyId.Value;
                var list = await repo.GetAllByCompanyAsync(cid, searchTerm, searchBy, page, limit);
                var total = await repo.GetCountByCompanyAsync(cid, searchTerm, searchBy);

                var dtos = new List<CheckRequestDto>();
                foreach (var r in list)
                {
                    var dto = new CheckRequestDto
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
                        Phone = r.Phone,
                        Status = r.Status,
                        Reason = r.Reason,
                        CreatedAt = r.CreatedAt,
                        UpdatedAt = r.UpdatedAt,
                        LineItems = r.LineItems
                                             .Select(li => new CheckRequestLineItemDto
                                             {
                                                 Id = li.Id,
                                                 Dirham = li.Dirham,
                                                 Lyd = li.Lyd
                                             }).ToList(),
                        RepresentativeId = r.RepresentativeId
                    };

                    // **Populate the Representative DTO if we have an ID**
                    if (r.RepresentativeId.HasValue)
                    {
                        var rep = await repRepo.GetByIdAsync(r.RepresentativeId.Value);
                        if (rep != null)
                        {
                            dto.Representative = new RepresentativeDto
                            {
                                Id = rep.Id,
                                Name = rep.Name,
                                Number = rep.Number,
                                // any other fields you need…
                            };
                        }
                    }

                    dtos.Add(dto);
                }

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
    IRepresentativeRepository repRepo,    // ← add this
    IUserRepository userRepo,
    ILogger<CheckRequestEndpoints> log)
        {
            log.LogInformation("GetCompanyRequestById({Id})", id);
            try
            {
                // 1️⃣ Authenticate
                var raw = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? ctx.User.FindFirst("nameid")?.Value;
                if (!int.TryParse(raw, out var authId))
                    return Results.Unauthorized();

                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null || !me.CompanyId.HasValue)
                    return Results.Unauthorized();

                // 2️⃣ Load the request
                var ent = await repo.GetByIdAsync(id);
                if (ent == null || ent.CompanyId != me.CompanyId.Value)
                    return Results.NotFound("Check request not found.");

                // 3️⃣ Load its representative, if any
                Representative? rep = null;
                if (ent.RepresentativeId.HasValue)
                {
                    rep = await repRepo.GetByIdAsync(ent.RepresentativeId.Value);
                    if (rep == null)
                        log.LogWarning("Representative {RepId} not found", ent.RepresentativeId);
                }

                // 4️⃣ Project to DTO
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
                    Phone = ent.Phone,
                    Status = ent.Status,
                    Reason = ent.Reason,
                    CreatedAt = ent.CreatedAt,
                    UpdatedAt = ent.UpdatedAt,

                    RepresentativeId = ent.RepresentativeId,
                    Representative = rep == null
                        ? null
                        : new RepresentativeDto
                        {
                            Id = rep.Id,
                            Name = rep.Name,
                            Number = rep.Number,
                            PassportNumber = rep.PassportNumber,
                            IsActive = rep.IsActive,
                            IsDeleted = rep.IsDeleted,
                            PhotoUrl = rep.PhotoUrl,
                            CreatedAt = rep.CreatedAt,
                            UpdatedAt = rep.UpdatedAt
                        },

                    LineItems = ent.LineItems
                                  .Select(li => new CheckRequestLineItemDto
                                  {
                                      Id = li.Id,
                                      Dirham = li.Dirham,
                                      Lyd = li.Lyd
                                  })
                                  .ToList()
                };

                return Results.Ok(dto);
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Auth error in GetCompanyRequestById");
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
                    Phone = dto.Phone,
                    Status = "Pending",
                    RepresentativeId = dto.RepresentativeId,
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
                    Phone = ent.Phone,
                    Status = ent.Status,
                    RepresentativeId = ent.RepresentativeId ?? 0,
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

        public static async Task<IResult> UpdateCompanyRequest(
    int id,
    [FromBody] CheckRequestCreateDto dto,
    HttpContext ctx,
    ICheckRequestRepository repo,
    IUserRepository userRepo,
    IValidator<CheckRequestCreateDto> validator,
    ILogger<CheckRequestEndpoints> log)
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
                ent.Branch = dto.Branch;
                ent.BranchNum = dto.BranchNum;
                ent.Date = dto.Date;
                ent.CustomerName = dto.CustomerName;
                ent.CardNum = dto.CardNum;
                ent.AccountNum = dto.AccountNum;
                ent.Beneficiary = dto.Beneficiary;
                ent.Phone = dto.Phone;
                ent.RepresentativeId = dto.RepresentativeId;

                // replace line items
                ent.LineItems = dto.LineItems
                    .Select(li => new CheckRequestLineItem { Dirham = li.Dirham, Lyd = li.Lyd })
                    .ToList();

                await repo.UpdateAsync(ent);
                log.LogInformation("Updated CheckRequest Id={Id}", id);

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
                    Phone = ent.Phone,
                    Status = ent.Status,
                    Reason = ent.Reason,
                    RepresentativeId = ent.RepresentativeId ?? 0,
                    LineItems = ent.LineItems
                                        .Select(li => new CheckRequestLineItemDto { Id = li.Id, Dirham = li.Dirham, Lyd = li.Lyd })
                                        .ToList(),
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


        // ── ADMIN: list all ───────────────────────────────────────────
        public static async Task<IResult> AdminGetAll(
          ICheckRequestRepository repo,
          IRepresentativeRepository repRepo,
          ILogger<CheckRequestEndpoints> log,
          [FromQuery] string? searchTerm,
          [FromQuery] string? searchBy,
          [FromQuery] int page = 1,
          [FromQuery] int limit = 50)
        {
            var list = await repo.GetAllAsync(searchTerm, searchBy, page, limit);
            var total = await repo.GetCountAsync(searchTerm, searchBy);

            var dtos = new List<CheckRequestDto>();
            foreach (var r in list)
            {
                var dto = new CheckRequestDto
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
                    Phone = r.Phone,
                    Status = r.Status,
                    Reason = r.Reason,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    LineItems = r.LineItems
                                            .Select(li => new CheckRequestLineItemDto
                                            {
                                                Id = li.Id,
                                                Dirham = li.Dirham,
                                                Lyd = li.Lyd
                                            }).ToList(),
                    RepresentativeId = r.RepresentativeId
                };

                // **Populate the Representative DTO if we have an ID**
                if (r.RepresentativeId.HasValue)
                {
                    var rep = await repRepo.GetByIdAsync(r.RepresentativeId.Value);
                    if (rep != null)
                    {
                        dto.Representative = new RepresentativeDto
                        {
                            Id = rep.Id,
                            Name = rep.Name,
                            Number = rep.Number
                        };
                    }
                }

                dtos.Add(dto);
            }

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
    [FromServices] ICheckRequestRepository repo,
    [FromServices] IRepresentativeRepository repRepo,   // ← add this
    [FromServices] ILogger<CheckRequestEndpoints> log)
        {
            log.LogInformation("AdminGetById({Id})", id);

            // 1️⃣ Load the request
            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
            {
                log.LogWarning("CheckRequest {Id} not found", id);
                return Results.NotFound("Check request not found.");
            }

            // 2️⃣ Load its representative, if any
            Representative? rep = null;
            if (ent.RepresentativeId.HasValue)
            {
                rep = await repRepo.GetByIdAsync(ent.RepresentativeId.Value);
                if (rep == null)
                    log.LogWarning("Representative {RepId} not found", ent.RepresentativeId);
            }

            // 3️⃣ Project to DTO
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
                Phone = ent.Phone,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt,

                RepresentativeId = ent.RepresentativeId,
                Representative = rep == null
                    ? null
                    : new RepresentativeDto
                    {
                        Id = rep.Id,
                        Name = rep.Name,
                        Number = rep.Number,
                        PassportNumber = rep.PassportNumber,
                        IsActive = rep.IsActive,
                        IsDeleted = rep.IsDeleted,
                        PhotoUrl = rep.PhotoUrl,
                        CreatedAt = rep.CreatedAt,
                        UpdatedAt = rep.UpdatedAt
                    },

                LineItems = ent.LineItems
                              .Select(li => new CheckRequestLineItemDto
                              {
                                  Id = li.Id,
                                  Dirham = li.Dirham,
                                  Lyd = li.Lyd
                              })
                              .ToList()
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
            ent.Reason = dto.Reason;
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
                Phone = ent.Phone,
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
