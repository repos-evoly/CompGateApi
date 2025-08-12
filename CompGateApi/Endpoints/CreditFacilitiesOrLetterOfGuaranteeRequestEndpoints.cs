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
    public class CreditFacilitiesOrLetterOfGuaranteeRequestEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var company = app
                .MapGroup("/api/creditfacilities")
                .WithTags("CreditFacilities")
                .RequireAuthorization("RequireCompanyUser");
            // .RequireAuthorization("CanRequestCreditFacilities");

            company.MapGet("/", GetMyRequests)
                   .Produces<PagedResult<CreditFacilitiesOrLetterOfGuaranteeRequestDto>>(200);

            company.MapGet("/{id:int}", GetMyById)
                   .Produces<CreditFacilitiesOrLetterOfGuaranteeRequestDto>(200)
                   .Produces(404);

            company.MapPost("/", CreateRequest)
                   .Accepts<CreditFacilitiesOrLetterOfGuaranteeRequestCreateDto>("application/json")
                   .Produces<CreditFacilitiesOrLetterOfGuaranteeRequestDto>(201)
                   .Produces(400);

            company.MapPut("/{id:int}", UpdateRequest)
                .WithName("UpdateCreditFacilitiesRequest")
                .Accepts<CreditFacilitiesOrLetterOfGuaranteeRequestCreateDto>("application/json")
                .Produces<CreditFacilitiesOrLetterOfGuaranteeRequestDto>(200)
                .Produces(400)
                .Produces(404);


            var admin = app
                .MapGroup("/api/admin/creditfacilities")
                .WithTags("CreditFacilities")
                .RequireAuthorization("RequireAdminUser");
            // .RequireAuthorization("AdminAccess");

            admin.MapGet("/", GetAllAdmin)
                 .Produces<PagedResult<CreditFacilitiesOrLetterOfGuaranteeRequestDto>>(200);

            admin.MapGet("/{id:int}", AdminGetById)
                 .Produces<CreditFacilitiesOrLetterOfGuaranteeRequestDto>(200)
                 .Produces(404);

            admin.MapPut("/{id:int}/status", UpdateStatus)
                 .Accepts<CreditFacilitiesOrLetterOfGuaranteeRequestStatusUpdateDto>("application/json")
                 .Produces<CreditFacilitiesOrLetterOfGuaranteeRequestDto>(200)
                 .Produces(404)
                 .Produces(400);
        }

        static bool TryGetAuthUserId(HttpContext ctx, out int userId)
        {
            userId = 0;
            var raw = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? ctx.User.FindFirstValue("nameid");
            return int.TryParse(raw, out userId);
        }

        public static async Task<IResult> GetMyRequests(
            HttpContext ctx,
            ICreditFacilitiesOrLetterOfGuaranteeRequestRepository repo,
            IUserRepository userRepo,
            ILogger<CreditFacilitiesOrLetterOfGuaranteeRequestEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            if (!TryGetAuthUserId(ctx, out var auth))
                return Results.Unauthorized();

            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? string.Empty;
            var me = await userRepo.GetUserByAuthId(auth, bearer);
            if (me == null) return Results.Unauthorized();

            var total = await repo.GetCountByUserAsync(me.UserId, searchTerm, searchBy);
            var list = await repo.GetAllByUserAsync(me.UserId, searchTerm, searchBy, page, limit);

            var dtos = list.Select(r => new CreditFacilitiesOrLetterOfGuaranteeRequestDto
            {
                Id = r.Id,
                UserId = r.UserId,
                AccountNumber = r.AccountNumber,
                Date = r.Date,
                Amount = r.Amount,
                Purpose = r.Purpose,
                AdditionalInfo = r.AdditionalInfo,
                Curr = r.Curr,
                ReferenceNumber = r.ReferenceNumber,
                Type = r.Type,
                Status = r.Status,
                Reason = r.Reason,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            return Results.Ok(new PagedResult<CreditFacilitiesOrLetterOfGuaranteeRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit)
            });
        }

        public static async Task<IResult> GetMyById(
            int id,
            HttpContext ctx,
            ICreditFacilitiesOrLetterOfGuaranteeRequestRepository repo,
            IUserRepository userRepo)
        {
            if (!TryGetAuthUserId(ctx, out var auth))
                return Results.Unauthorized();

            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? string.Empty;
            var me = await userRepo.GetUserByAuthId(auth, bearer);
            if (me == null) return Results.Unauthorized();

            var ent = await repo.GetByIdAsync(id);
            if (ent == null || ent.UserId != me.UserId)
                return Results.NotFound();

            var dto = new CreditFacilitiesOrLetterOfGuaranteeRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                AccountNumber = ent.AccountNumber,
                Date = ent.Date,
                Amount = ent.Amount,
                Purpose = ent.Purpose,
                AdditionalInfo = ent.AdditionalInfo,
                Curr = ent.Curr,
                ReferenceNumber = ent.ReferenceNumber,
                Type = ent.Type,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Ok(dto);
        }

        public static async Task<IResult> CreateRequest(
            [FromBody] CreditFacilitiesOrLetterOfGuaranteeRequestCreateDto dto,
            HttpContext ctx,
            ICreditFacilitiesOrLetterOfGuaranteeRequestRepository repo,
            IUserRepository userRepo)
        // IValidator<CreditFacilitiesOrLetterOfGuaranteeRequestCreateDto> validator)
        {
            // var validation = await validator.ValidateAsync(dto);
            // if (!validation.IsValid)
            //     return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));

            if (!TryGetAuthUserId(ctx, out var auth))
                return Results.Unauthorized();

            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? string.Empty;
            var me = await userRepo.GetUserByAuthId(auth, bearer);
            if (me == null) return Results.Unauthorized();

            if (me.CompanyId == null) return Results.Unauthorized();

            var ent = new CreditFacilitiesOrLetterOfGuaranteeRequest
            {
                UserId = me.UserId,
                AccountNumber = dto.AccountNumber,
                CompanyId = me.CompanyId.Value,
                Date = dto.Date,
                Amount = dto.Amount,
                Purpose = dto.Purpose,
                AdditionalInfo = dto.AdditionalInfo,
                Curr = dto.Curr,
                ReferenceNumber = dto.ReferenceNumber,
                Type = dto.Type,
                Status = "Pending"
            };

            await repo.CreateAsync(ent);

            var outDto = new CreditFacilitiesOrLetterOfGuaranteeRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                AccountNumber = ent.AccountNumber,
                Date = ent.Date,
                Amount = ent.Amount,
                Purpose = ent.Purpose,
                AdditionalInfo = ent.AdditionalInfo,
                Curr = ent.Curr,
                ReferenceNumber = ent.ReferenceNumber,
                Type = ent.Type,
                Status = ent.Status,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Created($"/api/creditfacilities/{ent.Id}", outDto);
        }

        public static async Task<IResult> UpdateRequest(
    int id,
    [FromBody] CreditFacilitiesOrLetterOfGuaranteeRequestCreateDto dto,
    HttpContext ctx,
    ICreditFacilitiesOrLetterOfGuaranteeRequestRepository repo,
    IUserRepository userRepo,
    ILogger<CreditFacilitiesOrLetterOfGuaranteeRequestEndpoints> log)
        {
            log.LogInformation("UpdateRequest payload: {@Dto}", dto);

            // auth
            if (!TryGetAuthUserId(ctx, out var auth))
                return Results.Unauthorized();

            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? string.Empty;
            var me = await userRepo.GetUserByAuthId(auth, bearer);
            if (me == null || me.CompanyId == null)
                return Results.Unauthorized();

            // fetch entity
            var ent = await repo.GetByIdAsync(id);
            if (ent == null || ent.CompanyId != me.CompanyId.Value)
                return Results.NotFound();

            if (ent.Status.Equals("printed", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("Cannot edit a printed form.");

            // update fields
            ent.AccountNumber = dto.AccountNumber;
            ent.Date = dto.Date;
            ent.Amount = dto.Amount;
            ent.Purpose = dto.Purpose;
            ent.AdditionalInfo = dto.AdditionalInfo;
            ent.Curr = dto.Curr;
            ent.ReferenceNumber = dto.ReferenceNumber;
            ent.Type = dto.Type;
            // leave Status/Reason untouched

            await repo.UpdateAsync(ent);
            log.LogInformation("Updated CreditFacilities request Id={Id}", id);

            var outDto = new CreditFacilitiesOrLetterOfGuaranteeRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                AccountNumber = ent.AccountNumber,
                Date = ent.Date,
                Amount = ent.Amount,
                Purpose = ent.Purpose,
                AdditionalInfo = ent.AdditionalInfo,
                Curr = ent.Curr,
                ReferenceNumber = ent.ReferenceNumber,
                Type = ent.Type,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };

            return Results.Ok(outDto);
        }


        public static async Task<IResult> GetAllAdmin(
            ICreditFacilitiesOrLetterOfGuaranteeRequestRepository repo,
            ILogger<CreditFacilitiesOrLetterOfGuaranteeRequestEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            var total = await repo.GetCountAsync(searchTerm, searchBy);
            var list = await repo.GetAllAsync(searchTerm, searchBy, page, limit);

            var dtos = list.Select(ent => new CreditFacilitiesOrLetterOfGuaranteeRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                AccountNumber = ent.AccountNumber,
                Date = ent.Date,
                Amount = ent.Amount,
                Purpose = ent.Purpose,
                AdditionalInfo = ent.AdditionalInfo,
                Curr = ent.Curr,
                ReferenceNumber = ent.ReferenceNumber,
                Type = ent.Type,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            }).ToList();

            return Results.Ok(new PagedResult<CreditFacilitiesOrLetterOfGuaranteeRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit)
            });
        }

        public static async Task<IResult> AdminGetById(
            int id,
            ICreditFacilitiesOrLetterOfGuaranteeRequestRepository repo)
        {
            var ent = await repo.GetByIdAsync(id);
            if (ent == null) return Results.NotFound();

            var dto = new CreditFacilitiesOrLetterOfGuaranteeRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                AccountNumber = ent.AccountNumber,
                Date = ent.Date,
                Amount = ent.Amount,
                Purpose = ent.Purpose,
                AdditionalInfo = ent.AdditionalInfo,
                Curr = ent.Curr,
                ReferenceNumber = ent.ReferenceNumber,
                Type = ent.Type,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Ok(dto);
        }

        public static async Task<IResult> UpdateStatus(
            int id,
            [FromBody] CreditFacilitiesOrLetterOfGuaranteeRequestStatusUpdateDto dto,
            ICreditFacilitiesOrLetterOfGuaranteeRequestRepository repo)
        // IValidator<CreditFacilitiesOrLetterOfGuaranteeRequestStatusUpdateDto> validator)
        {
            // var res = await validator.ValidateAsync(dto);
            // if (!res.IsValid)
            //     return Results.BadRequest(res.Errors.Select(e => e.ErrorMessage));

            var ent = await repo.GetByIdAsync(id);
            if (ent == null) return Results.NotFound();

            ent.Status = dto.Status;
            ent.Reason = dto.Reason;

            ent.UpdatedAt = DateTimeOffset.UtcNow;
            await repo.UpdateAsync(ent);

            var outDto = new CreditFacilitiesOrLetterOfGuaranteeRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                AccountNumber = ent.AccountNumber,
                Date = ent.Date,
                Amount = ent.Amount,
                Purpose = ent.Purpose,
                AdditionalInfo = ent.AdditionalInfo,
                Curr = ent.Curr,
                ReferenceNumber = ent.ReferenceNumber,
                Type = ent.Type,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Ok(outDto);
        }
    }
}