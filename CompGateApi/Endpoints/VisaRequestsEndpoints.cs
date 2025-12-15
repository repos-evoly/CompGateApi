// ── CompGateApi.Endpoints/VisaRequestEndpoints.cs ──────────────────────
using System.Security.Claims;
using System.Text.Json;
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

            // Create = save only (NO DEBIT HERE)
            company.MapPost("/", CreateMyRequest)
                   .Accepts<IFormFile>("multipart/form-data")
                   .Produces<VisaRequestDto>(201)
                   .Produces(400);

            company.MapPut("/{id:int}", UpdateMyRequest)
                   .Accepts<IFormFile>("multipart/form-data")
                   .Produces<VisaRequestDto>(200)
                   .Produces(400)
                   .Produces(404);
            // POST alias for update
            company.MapPost("/{id:int}/update", UpdateMyRequest)
                   .Accepts<IFormFile>("multipart/form-data")
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

            // Admin approves = DEBIT; rejects = REFUND (if already debited)
            admin.MapPut("/{id:int}/status", UpdateStatus)
                 .Accepts<VisaRequestStatusUpdateDto>("application/json")
                 .Produces<VisaRequestDto>(200)
                 .Produces(404);
            // POST alias for status update
            admin.MapPost("/{id:int}/status/update", UpdateStatus)
                 .Accepts<VisaRequestStatusUpdateDto>("application/json")
                 .Produces<VisaRequestDto>(200)
                 .Produces(404);

            admin.MapGet("/{id:int}", GetByIdAdmin)
                 .Produces<VisaRequestDto>(200)
                 .Produces(404);
        }

        private static int GetAuthUserId(HttpContext ctx)
        {
            var raw = ctx.User.FindFirst("nameid")?.Value
                   ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(raw, out var id)) return id;
            throw new UnauthorizedAccessException("Missing/invalid 'nameid' claim.");
        }

    
        // ───────────────────────────────────────────────────────────────────
        // Company – List
        // ───────────────────────────────────────────────────────────────────
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
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            var list = await repo.GetAllByCompanyAsync(me.CompanyId.Value, searchTerm, searchBy, page, limit);
            var total = await repo.GetCountByCompanyAsync(me.CompanyId.Value, searchTerm, searchBy);

            var dtos = list.Select(v => new VisaRequestDto
            {
                Id = v.Id,
                UserId = v.UserId,
                CompanyId = v.CompanyId,
                VisaId = v.VisaId,
                Quantity = v.Quantity,
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

        // ───────────────────────────────────────────────────────────────────
        // Company – Get one
        // ───────────────────────────────────────────────────────────────────
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

            var ent = await repo.GetByIdAsync(id); // includes Attachments
            if (ent == null || !me.CompanyId.HasValue || ent.CompanyId != me.CompanyId.Value)
                return Results.NotFound();

            var dto = new VisaRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                CompanyId = ent.CompanyId,
                VisaId = ent.VisaId,
                Quantity = ent.Quantity,
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
                Attachments = ent.Attachments.Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    AttFileName = a.AttFileName,
                    AttOriginalFileName = a.AttOriginalFileName,
                    AttMime = a.AttMime,
                    AttSize = a.AttSize,
                    AttUrl = a.AttUrl,
                    Description = a.Description,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt
                }).ToList(),
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };

            return Results.Ok(dto);
        }

        // ───────────────────────────────────────────────────────────────────
        // Company – Create (NO DEBIT HERE)
        // ───────────────────────────────────────────────────────────────────
        public static async Task<IResult> CreateMyRequest(
           HttpRequest req,
           HttpContext ctx,
           IVisaRequestRepository repo,
           IAttachmentRepository attRepo,
           IUserRepository userRepo,
           IValidator<VisaRequestCreateDto> validator,
           ILogger<VisaRequestEndpoints> log)
        {
            log.LogInformation("Visa CreateMyRequest (multipart/form-data)");

            var raw = ctx.User.FindFirst("nameid")?.Value
                   ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(raw, out var authId))
                return Results.Unauthorized();
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            if (!req.HasFormContentType)
                return Results.BadRequest("Must be multipart/form-data.");
            var form = await req.ReadFormAsync();
            var dtoJson = form["Dto"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(dtoJson))
                return Results.BadRequest("Missing 'Dto' field.");

            VisaRequestCreateDto dto;
            try
            {
                dto = JsonSerializer.Deserialize<VisaRequestCreateDto>(
                    dtoJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                )!;
            }
            catch (JsonException)
            {
                return Results.BadRequest("Invalid JSON in 'Dto' field.");
            }

            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));

            // Only persist (no debit)
            var ent = new VisaRequest
            {
                UserId = me.UserId,
                CompanyId = me.CompanyId.Value,
                VisaId = dto.VisaId,
                Quantity = dto.Quantity <= 0 ? 1 : dto.Quantity,
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
            log.LogInformation("Persisted VisaRequest Id={RequestId}", ent.Id);

            var attachments = new List<AttachmentDto>();
            foreach (var file in form.Files)
            {
                var attDto = await attRepo.Upload(
                    file,
                    me.CompanyId.Value,
                    subject: $"VisaRequest {ent.Id}",
                    description: "",
                    createdBy: me.UserId.ToString()
                );

                await attRepo.LinkToVisaRequestAsync(attDto.Id, ent.Id);
                attachments.Add(attDto);
            }

            var outDto = new VisaRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                CompanyId = ent.CompanyId,
                VisaId = ent.VisaId,
                Quantity = ent.Quantity,
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
                Attachments = attachments,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };

            return Results.Created($"/api/visarequests/{ent.Id}", outDto);
        }

        // ───────────────────────────────────────────────────────────────────
        // Company – Update metadata (NO DEBIT)
        // ───────────────────────────────────────────────────────────────────
        public static async Task<IResult> UpdateMyRequest(
            int id,
            HttpRequest req,
            HttpContext ctx,
            IVisaRequestRepository repo,
            IAttachmentRepository attRepo,
            IUserRepository userRepo,
            IValidator<VisaRequestCreateDto> validator,
            ILogger<VisaRequestEndpoints> log)
        {
            log.LogInformation("Visa UpdateMyRequest Id={Id} (multipart/form-data)", id);

            var raw = ctx.User.FindFirst("nameid")?.Value
                   ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(raw, out var authId))
                return Results.Unauthorized();
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            if (!req.HasFormContentType)
                return Results.BadRequest("Must be multipart/form-data.");
            var form = await req.ReadFormAsync();

            var dtoJson = form["Dto"].FirstOrDefault();
            if (string.IsNullOrEmpty(dtoJson))
                return Results.BadRequest("Missing 'Dto' field.");

            VisaRequestCreateDto dto;
            try
            {
                dto = JsonSerializer.Deserialize<VisaRequestCreateDto>(dtoJson,
                      new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            }
            catch
            {
                return Results.BadRequest("Invalid JSON in 'Dto' field.");
            }

            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));

            var ent = await repo.GetByIdAsync(id); // includes Attachments
            if (ent == null || ent.CompanyId != me.CompanyId.Value)
                return Results.NotFound();
            if (ent.Status.Equals("printed", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("Cannot edit a printed request.");

            // update metadata (no debit)
            ent.VisaId = dto.VisaId;
            ent.Quantity = dto.Quantity <= 0 ? 1 : dto.Quantity;
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

            await repo.UpdateAsync(ent);

            if (form.Files.Count > 0)
            {
                foreach (var file in form.Files)
                {
                    var attDto = await attRepo.Upload(
                        file,
                        me.CompanyId.Value,
                        subject: $"VisaRequest {ent.Id}",
                        description: "",
                        createdBy: me.UserId.ToString()
                    );

                    await attRepo.LinkToVisaRequestAsync(attDto.Id, ent.Id);
                }
            }

            var outDto = new VisaRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                CompanyId = ent.CompanyId,
                VisaId = ent.VisaId,
                Quantity = ent.Quantity,
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
                Attachments = ent.Attachments.Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    AttFileName = a.AttFileName,
                    AttOriginalFileName = a.AttOriginalFileName,
                    AttMime = a.AttMime,
                    AttSize = a.AttSize,
                    AttUrl = a.AttUrl,
                    Description = a.Description,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt
                }).ToList(),
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };

            return Results.Ok(outDto);
        }

        // ───────────────────────────────────────────────────────────────────
        // Admin – List
        // ───────────────────────────────────────────────────────────────────
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
                CompanyId = v.CompanyId,
                VisaId = v.VisaId,
                Quantity = v.Quantity,
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

        // ───────────────────────────────────────────────────────────────────
        // Admin – Update status:
        //    - Approved → DEBIT (if not yet debited)
        //    - Rejected → REFUND (if already debited)
        // ───────────────────────────────────────────────────────────────────
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
            ent.Reason = dto.Reason;

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




        // ───────────────────────────────────────────────────────────────────
        // Admin – Get one
        // ───────────────────────────────────────────────────────────────────
        public static async Task<IResult> GetByIdAdmin(
            int id,
            [FromServices] IVisaRequestRepository repo,
            [FromServices] ILogger<VisaRequestEndpoints> log)
        {
            log.LogInformation("Admin:GetById VisaRequest {Id}", id);

            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound($"VisaRequest {id} not found.");

            var dto = new VisaRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                CompanyId = ent.CompanyId,
                VisaId = ent.VisaId,
                Quantity = ent.Quantity,
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
                Attachments = ent.Attachments.Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    AttFileName = a.AttFileName,
                    AttOriginalFileName = a.AttOriginalFileName,
                    AttMime = a.AttMime,
                    AttSize = a.AttSize,
                    AttUrl = a.AttUrl,
                    Description = a.Description,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt
                }).ToList(),
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };

            return Results.Ok(dto);
        }
    }
}
