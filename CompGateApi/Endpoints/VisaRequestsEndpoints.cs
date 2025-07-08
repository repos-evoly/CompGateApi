// ── CompGateApi.Endpoints/VisaRequestEndpoints.cs ──────────────────────
using System.Security.Claims;
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
    public class VisaRequestEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var company = app
                .MapGroup("/api/visarequests")
                .WithTags("VisaRequests")
                .RequireAuthorization("RequireCompanyUser");

            company.MapGet("/", GetMyRequests)
                   .Produces<PagedResult<VisaRequestDto>>(200);

            company.MapGet("/{id:int}", GetMyById)
                   .Produces<VisaRequestDto>(200)
                   .Produces(404);

            company.MapPost("/", CreateMyRequest)
                   .Accepts<VisaRequestCreateDto>("application/json")
                   .Produces<VisaRequestDto>(201)
                   .Produces(400);

            company.MapPut("/{id:int}", UpdateMyRequest)
                .WithName("UpdateVisaRequest")
                .Accepts<VisaRequestCreateDto>("application/json")
                .Produces<VisaRequestDto>(200)
                .Produces(400)
                .Produces(404);


            var admin = app
                .MapGroup("/api/admin/visarequests")
                .WithTags("VisaRequests")
                .RequireAuthorization("RequireAdminUser")
                .RequireAuthorization("AdminAccess");

            admin.MapGet("/", GetAllAdmin)
                 .Produces<PagedResult<VisaRequestDto>>(200);

            admin.MapPut("/{id:int}/status", UpdateStatus)
                 .Accepts<VisaRequestStatusUpdateDto>("application/json")
                 .Produces<VisaRequestDto>(200)
                 .Produces(404);

            admin.MapGet("/{id:int}", GetByIdAdmin)
                .Produces<VisaRequestDto>(200)
                .Produces(404);
        }

        private static int GetAuthUserId(HttpContext ctx)
        {
            // token uses "nameid" claim
            var raw = ctx.User.FindFirst("nameid")?.Value;
            if (int.TryParse(raw, out var id)) return id;
            throw new UnauthorizedAccessException("Missing/invalid 'nameid' claim.");
        }

        public static async Task<IResult> GetMyRequests(
            HttpContext ctx,
            [FromServices] IVisaRequestRepository repo,
            [FromServices] IUserRepository userRepo,
            [FromServices] ILogger<VisaRequestEndpoints> log,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? searchBy = null)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null)
                return Results.Unauthorized();

            if (!me.CompanyId.HasValue)
                return Results.Unauthorized();

            var list = await repo.GetAllByCompanyAsync(me.CompanyId.Value, searchTerm, searchBy, page, limit);
            var total = await repo.GetCountByCompanyAsync(me.CompanyId.Value, searchTerm, searchBy);


            var dtos = list.Select(v => new VisaRequestDto
            {
                Id = v.Id,
                UserId = v.UserId,
                Branch = v.Branch,
                Date = v.Date,
                AccountHolderName = v.AccountHolderName,
                AccountNumber = v.AccountNumber,
                NationalId = v.NationalId,
                PhoneNumberLinkedToNationalId = v.PhoneNumberLinkedToNationalId,
                Cbl = v.Cbl,
                CardMovementApproval = v.CardMovementApproval,
                CardUsingAcknowledgment = v.CardUsingAcknowledgment,
                ForeignAmount = v.ForeignAmount,
                LocalAmount = v.LocalAmount,
                Pldedge = v.Pldedge,
                Status = v.Status,
                Reason = v.Reason,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt
            }).ToList();

            return Results.Ok(new PagedResult<VisaRequestDto>
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
            [FromServices] IVisaRequestRepository repo,
            [FromServices] IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null)
                return Results.Unauthorized();

            var v = await repo.GetByIdAsync(id);
            if (v == null || !me.CompanyId.HasValue || v.CompanyId != me.CompanyId.Value)
                return Results.NotFound();

            var dto = new VisaRequestDto
            {
                Id = v.Id,
                UserId = v.UserId,
                Branch = v.Branch,
                Date = v.Date,
                AccountHolderName = v.AccountHolderName,
                AccountNumber = v.AccountNumber,
                NationalId = v.NationalId,
                PhoneNumberLinkedToNationalId = v.PhoneNumberLinkedToNationalId,
                Cbl = v.Cbl,
                CardMovementApproval = v.CardMovementApproval,
                CardUsingAcknowledgment = v.CardUsingAcknowledgment,
                ForeignAmount = v.ForeignAmount,
                LocalAmount = v.LocalAmount,
                Pldedge = v.Pldedge,
                Status = v.Status,
                Reason = v.Reason,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt
            };
            return Results.Ok(dto);
        }

        public static async Task<IResult> CreateMyRequest(
            [FromBody] VisaRequestCreateDto dto,
            HttpContext ctx,
            [FromServices] IVisaRequestRepository repo,
            [FromServices] IUserRepository userRepo,
            [FromServices] IValidator<VisaRequestCreateDto> validator,
            [FromServices] ILogger<VisaRequestEndpoints> log)
        {
            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));

            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null)
                return Results.Unauthorized();

            if (!me.CompanyId.HasValue)
                return Results.Unauthorized();

            var ent = new VisaRequest
            {
                UserId = me.UserId,
                CompanyId = me.CompanyId.Value,
                Branch = dto.Branch,
                Date = dto.Date,
                AccountHolderName = dto.AccountHolderName,
                AccountNumber = dto.AccountNumber,
                NationalId = dto.NationalId,
                PhoneNumberLinkedToNationalId = dto.PhoneNumberLinkedToNationalId,
                Cbl = dto.Cbl,
                CardMovementApproval = dto.CardMovementApproval,
                CardUsingAcknowledgment = dto.CardUsingAcknowledgment,
                ForeignAmount = dto.ForeignAmount,
                LocalAmount = dto.LocalAmount,
                Pldedge = dto.Pldedge,
                Status = "Pending"
            };

            await repo.CreateAsync(ent);
            log.LogInformation("Created VisaRequest Id={Id}", ent.Id);

            var outDto = new VisaRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                Branch = ent.Branch,
                Date = ent.Date,
                AccountHolderName = ent.AccountHolderName,
                AccountNumber = ent.AccountNumber,
                NationalId = ent.NationalId,
                PhoneNumberLinkedToNationalId = ent.PhoneNumberLinkedToNationalId,
                Cbl = ent.Cbl,
                CardMovementApproval = ent.CardMovementApproval,
                CardUsingAcknowledgment = ent.CardUsingAcknowledgment,
                ForeignAmount = ent.ForeignAmount,
                LocalAmount = ent.LocalAmount,
                Pldedge = ent.Pldedge,
                Status = ent.Status,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Created($"/api/visarequests/{ent.Id}", outDto);
        }

        public static async Task<IResult> UpdateMyRequest(
    int id,
    [FromBody] VisaRequestCreateDto dto,
    HttpContext ctx,
    IVisaRequestRepository repo,
    IUserRepository userRepo,
    IValidator<VisaRequestCreateDto> validator,
    ILogger<VisaRequestEndpoints> log)
        {
            log.LogInformation("UpdateMyRequest payload: {@Dto}", dto);

            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));

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
            ent.Date = dto.Date;
            ent.AccountHolderName = dto.AccountHolderName;
            ent.AccountNumber = dto.AccountNumber;
            ent.NationalId = dto.NationalId;
            ent.PhoneNumberLinkedToNationalId = dto.PhoneNumberLinkedToNationalId;
            ent.Cbl = dto.Cbl;
            ent.CardMovementApproval = dto.CardMovementApproval;
            ent.CardUsingAcknowledgment = dto.CardUsingAcknowledgment;
            ent.ForeignAmount = dto.ForeignAmount;
            ent.LocalAmount = dto.LocalAmount;
            ent.Pldedge = dto.Pldedge;
            // leave Status and Reason unchanged

            await repo.UpdateAsync(ent);
            log.LogInformation("Updated VisaRequest Id={Id}", id);

            var outDto = new VisaRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                Branch = ent.Branch,
                Date = ent.Date,
                AccountHolderName = ent.AccountHolderName,
                AccountNumber = ent.AccountNumber,
                NationalId = ent.NationalId,
                PhoneNumberLinkedToNationalId = ent.PhoneNumberLinkedToNationalId,
                Cbl = ent.Cbl,
                CardMovementApproval = ent.CardMovementApproval,
                CardUsingAcknowledgment = ent.CardUsingAcknowledgment,
                ForeignAmount = ent.ForeignAmount,
                LocalAmount = ent.LocalAmount,
                Pldedge = ent.Pldedge,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };

            return Results.Ok(outDto);
        }


        public static async Task<IResult> GetAllAdmin(
            [FromServices] IVisaRequestRepository repo,
            [FromServices] ILogger<VisaRequestEndpoints> log,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50,
            [FromQuery] string? searchTerm = null)
        {
            var list = await repo.GetAllAsync(searchTerm, page, limit);
            var total = await repo.GetCountAsync(searchTerm);

            var dtos = list.Select(v => new VisaRequestDto
            {
                Id = v.Id,
                UserId = v.UserId,
                Branch = v.Branch,
                Date = v.Date,
                AccountHolderName = v.AccountHolderName,
                AccountNumber = v.AccountNumber,
                NationalId = v.NationalId,
                PhoneNumberLinkedToNationalId = v.PhoneNumberLinkedToNationalId,
                Cbl = v.Cbl,
                CardMovementApproval = v.CardMovementApproval,
                CardUsingAcknowledgment = v.CardUsingAcknowledgment,
                ForeignAmount = v.ForeignAmount,
                LocalAmount = v.LocalAmount,
                Pldedge = v.Pldedge,
                Status = v.Status,
                Reason = v.Reason,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt
            }).ToList();

            return Results.Ok(new PagedResult<VisaRequestDto>
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
            [FromBody] VisaRequestStatusUpdateDto dto,
            [FromServices] IVisaRequestRepository repo,
            [FromServices] IValidator<VisaRequestStatusUpdateDto> validator,
            [FromServices] ILogger<VisaRequestEndpoints> log)
        {
            var res = await validator.ValidateAsync(dto);
            if (!res.IsValid)
                return Results.BadRequest(res.Errors.Select(e => e.ErrorMessage));

            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound();

            ent.Status = dto.Status;
            await repo.UpdateAsync(ent);
            log.LogInformation("Updated VisaRequest {Id} to Status={Status}", id, dto.Status);

            var outDto = new VisaRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                Branch = ent.Branch,
                Date = ent.Date,
                AccountHolderName = ent.AccountHolderName,
                AccountNumber = ent.AccountNumber,
                NationalId = ent.NationalId,
                PhoneNumberLinkedToNationalId = ent.PhoneNumberLinkedToNationalId,
                Cbl = ent.Cbl,
                CardMovementApproval = ent.CardMovementApproval,
                CardUsingAcknowledgment = ent.CardUsingAcknowledgment,
                ForeignAmount = ent.ForeignAmount,
                LocalAmount = ent.LocalAmount,
                Pldedge = ent.Pldedge,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Ok(outDto);
        }
        public static async Task<IResult> GetByIdAdmin(
    int id,
    [FromServices] IVisaRequestRepository repo,
    [FromServices] ILogger<VisaRequestEndpoints> log)
        {
            log.LogInformation("Admin: GetByIdAdmin({Id})", id);

            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound($"VisaRequest {id} not found.");

            var dto = new VisaRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                Branch = ent.Branch,
                Date = ent.Date,
                AccountHolderName = ent.AccountHolderName,
                AccountNumber = ent.AccountNumber,
                NationalId = ent.NationalId,
                PhoneNumberLinkedToNationalId = ent.PhoneNumberLinkedToNationalId,
                Cbl = ent.Cbl,
                CardMovementApproval = ent.CardMovementApproval,
                CardUsingAcknowledgment = ent.CardUsingAcknowledgment,
                ForeignAmount = ent.ForeignAmount,
                LocalAmount = ent.LocalAmount,
                Pldedge = ent.Pldedge,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };

            return Results.Ok(dto);
        }

    }


}
