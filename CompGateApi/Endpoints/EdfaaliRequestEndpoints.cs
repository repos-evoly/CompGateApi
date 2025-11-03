using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CompGateApi.Endpoints
{
    public class EdfaaliRequestEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var company = app
                .MapGroup("/api/edfaalirequests")
                .WithTags("EdfaaliRequests")
                .RequireAuthorization("RequireCompanyUser");

            company.MapGet("/", GetCompanyRequests)
                   .Produces<PagedResult<EdfaaliRequestDto>>(200);

            company.MapGet("/{id:int}", GetCompanyRequestById)
                   .Produces<EdfaaliRequestDto>(200)
                   .Produces(404);

            company.MapPost("/", CreateCompanyRequest)
                   .Accepts<IFormFile>("multipart/form-data")
                   .Produces<EdfaaliRequestDto>(201)
                   .Produces(400);

            company.MapPut("/{id:int}", UpdateCompanyRequest)
                   .Accepts<IFormFile>("multipart/form-data")
                   .Produces<EdfaaliRequestDto>(200)
                   .Produces(400)
                   .Produces(404);

            var admin = app
                .MapGroup("/api/admin/edfaalirequests")
                .WithTags("EdfaaliRequests")
                .RequireAuthorization("RequireAdminUser");

            admin.MapGet("/", AdminGetAll)
                 .Produces<PagedResult<EdfaaliRequestDto>>(200);

            admin.MapGet("/{id:int}", AdminGetById)
                 .Produces<EdfaaliRequestDto>(200)
                 .Produces(404);

            admin.MapPut("/{id:int}/status", AdminUpdateStatus)
                 .Accepts<EdfaaliRequestStatusUpdateDto>("application/json")
                 .Produces<EdfaaliRequestDto>(200)
                 .Produces(400)
                 .Produces(404);
        }

        private static int GetAuthUserId(HttpContext ctx)
        {
            var raw = ctx.User.FindFirst("nameid")?.Value
                   ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(raw, out var id)) return id;
            throw new UnauthorizedAccessException("Missing/invalid 'nameid' claim.");
        }

        private static EdfaaliRequestDto ToDto(EdfaaliRequest e)
            => new()
            {
                Id = e.Id,
                UserId = e.UserId,
                CompanyId = e.CompanyId,
                RepresentativeId = e.RepresentativeId,
                NationalId = e.NationalId,
                IdentificationNumber = e.IdentificationNumber,
                IdentificationType = e.IdentificationType,
                CompanyEnglishName = e.CompanyEnglishName,
                WorkAddress = e.WorkAddress,
                StoreAddress = e.StoreAddress,
                City = e.City,
                Area = e.Area,
                Street = e.Street,
                MobileNumber = e.MobileNumber,
                ServicePhoneNumber = e.ServicePhoneNumber,
                BankAnnouncementPhoneNumber = e.BankAnnouncementPhoneNumber,
                Email = e.Email,
                AccountNumber = e.AccountNumber,
                Status = e.Status,
                Reason = e.Reason,
                Attachments = e.Attachments.Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    AttFileName = a.AttFileName,
                    AttOriginalFileName = a.AttOriginalFileName,
                    AttMime = a.AttMime,
                    AttSize = a.AttSize,
                    AttUrl = a.AttUrl,
                    Description = a.Description,
                    CreatedBy = a.CreatedBy,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt,
                    EdfaaliRequestId = a.EdfaaliRequestId
                }).ToList(),
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            };

        // COMPANY: list
        public static async Task<IResult> GetCompanyRequests(
            HttpContext ctx,
            IEdfaaliRequestRepository repo,
            IUserRepository userRepo,
            ILogger<EdfaaliRequestEndpoints> log,
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

                var total = await repo.GetCountByCompanyAsync(me.CompanyId.Value, searchTerm, searchBy);
                var list = await repo.GetAllByCompanyAsync(me.CompanyId.Value, searchTerm, searchBy, page, limit);
                var dtos = list.Select(ToDto).ToList();

                return Results.Ok(new PagedResult<EdfaaliRequestDto>
                {
                    Data = dtos,
                    Page = page,
                    Limit = limit,
                    TotalRecords = total,
                    TotalPages = (int)Math.Ceiling(total / (double)limit)
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        }

        // COMPANY: get by id
        public static async Task<IResult> GetCompanyRequestById(
            int id,
            HttpContext ctx,
            IEdfaaliRequestRepository repo,
            IUserRepository userRepo)
        {
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

                return Results.Ok(ToDto(ent));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        }

        // COMPANY: create (multipart with Dto JSON + files)
        public static async Task<IResult> CreateCompanyRequest(
            HttpRequest req,
            HttpContext ctx,
            IEdfaaliRequestRepository repo,
            IAttachmentRepository attRepo,
            IUserRepository userRepo,
            ILogger<EdfaaliRequestEndpoints> log)
        {
            if (!req.HasFormContentType)
                return Results.BadRequest("Must be multipart/form-data.");
            var form = await req.ReadFormAsync();

            var raw = ctx.User.FindFirst("nameid")?.Value;
            if (!int.TryParse(raw, out var authId))
                return Results.Unauthorized();
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            var dtoJson = form["Dto"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(dtoJson))
                return Results.BadRequest("Missing 'Dto' field.");

            EdfaaliRequestCreateDto dto;
            try
            {
                dto = JsonSerializer.Deserialize<EdfaaliRequestCreateDto>(dtoJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            }
            catch (JsonException)
            {
                return Results.BadRequest("Invalid JSON in 'Dto' field.");
            }

            int? representativeId = null;
            if (!string.IsNullOrWhiteSpace(dto.RepresentativeId) && int.TryParse(dto.RepresentativeId, out var rid))
                representativeId = rid;

            var ent = new EdfaaliRequest
            {
                UserId = me.UserId,
                CompanyId = me.CompanyId.Value,
                RepresentativeId = representativeId,
                NationalId = dto.NationalId,
                IdentificationNumber = dto.IdentificationNumber,
                IdentificationType = dto.IdentificationType,
                CompanyEnglishName = dto.CompanyEnglishName,
                WorkAddress = dto.WorkAddress,
                StoreAddress = dto.StoreAddress,
                City = dto.City,
                Area = dto.Area,
                Street = dto.Street,
                MobileNumber = dto.MobileNumber,
                ServicePhoneNumber = dto.ServicePhoneNumber,
                BankAnnouncementPhoneNumber = dto.BankAnnouncementPhoneNumber,
                Email = dto.Email,
                AccountNumber = dto.AccountNumber,
                Status = "Pending",
                Reason = string.Empty
            };
            await repo.CreateAsync(ent);
            log.LogInformation("Created EdfaaliRequest Id={Id}", ent.Id);

            // Upload and link all files
            var attachments = form.Files;
            foreach (var file in attachments)
            {
                var attDto = await attRepo.Upload(
                    file,
                    me.CompanyId.Value,
                    subject: $"EdfaaliRequest {ent.Id}",
                    description: string.Empty,
                    createdBy: me.UserId.ToString());
                await attRepo.LinkToEdfaaliRequestAsync(attDto.Id, ent.Id);
            }

            var fresh = await repo.GetByIdAsync(ent.Id);
            return Results.Created($"/api/edfaalirequests/{ent.Id}", ToDto(fresh!));
        }

        // COMPANY: update (multipart)
        public static async Task<IResult> UpdateCompanyRequest(
            int id,
            HttpRequest req,
            HttpContext ctx,
            IEdfaaliRequestRepository repo,
            IAttachmentRepository attRepo,
            IUserRepository userRepo,
            ILogger<EdfaaliRequestEndpoints> log)
        {
            if (!req.HasFormContentType)
                return Results.BadRequest("Must be multipart/form-data.");
            var form = await req.ReadFormAsync();

            var raw = ctx.User.FindFirst("nameid")?.Value;
            if (!int.TryParse(raw, out var authId))
                return Results.Unauthorized();
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            var ent = await repo.GetByIdAsync(id);
            if (ent == null || ent.CompanyId != me.CompanyId.Value)
                return Results.NotFound();
            if (ent.Status.Equals("printed", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("Cannot edit a printed form.");

            var dtoJson = form["Dto"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(dtoJson))
                return Results.BadRequest("Missing 'Dto' field.");

            EdfaaliRequestCreateDto dto;
            try
            {
                dto = JsonSerializer.Deserialize<EdfaaliRequestCreateDto>(dtoJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            }
            catch (JsonException)
            {
                return Results.BadRequest("Invalid JSON in 'Dto' field.");
            }

            ent.RepresentativeId = (!string.IsNullOrWhiteSpace(dto.RepresentativeId) && int.TryParse(dto.RepresentativeId, out var rid)) ? rid : null;
            ent.NationalId = dto.NationalId;
            ent.IdentificationNumber = dto.IdentificationNumber;
            ent.IdentificationType = dto.IdentificationType;
            ent.CompanyEnglishName = dto.CompanyEnglishName;
            ent.WorkAddress = dto.WorkAddress;
            ent.StoreAddress = dto.StoreAddress;
            ent.City = dto.City;
            ent.Area = dto.Area;
            ent.Street = dto.Street;
            ent.MobileNumber = dto.MobileNumber;
            ent.ServicePhoneNumber = dto.ServicePhoneNumber;
            ent.BankAnnouncementPhoneNumber = dto.BankAnnouncementPhoneNumber;
            ent.Email = dto.Email;
            ent.AccountNumber = dto.AccountNumber;

            await repo.UpdateAsync(ent);

            // New files only; keep existing ones
            if (form.Files.Count > 0)
            {
                foreach (var file in form.Files)
                {
                    var attDto = await attRepo.Upload(
                        file,
                        me.CompanyId.Value,
                        subject: $"EdfaaliRequest {ent.Id}",
                        description: string.Empty,
                        createdBy: me.UserId.ToString());
                    await attRepo.LinkToEdfaaliRequestAsync(attDto.Id, ent.Id);
                }
            }

            var fresh = await repo.GetByIdAsync(id);
            return Results.Ok(ToDto(fresh!));
        }

        // ADMIN: list all
        public static async Task<IResult> AdminGetAll(
            IEdfaaliRequestRepository repo,
            ILogger<EdfaaliRequestEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            var total = await repo.GetCountAsync(searchTerm, searchBy);
            var list = await repo.GetAllAsync(searchTerm, searchBy, page, limit);
            var dtos = list.Select(ToDto).ToList();
            return Results.Ok(new PagedResult<EdfaaliRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit)
            });
        }

        // ADMIN: get by id
        public static async Task<IResult> AdminGetById(
            int id,
            [FromServices] IEdfaaliRequestRepository repo,
            [FromServices] ILogger<EdfaaliRequestEndpoints> log)
        {
            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound();
            return Results.Ok(ToDto(ent));
        }

        // ADMIN: update status
        public static async Task<IResult> AdminUpdateStatus(
            int id,
            [FromBody] EdfaaliRequestStatusUpdateDto dto,
            [FromServices] IEdfaaliRequestRepository repo,
            [FromServices] ILogger<EdfaaliRequestEndpoints> log)
        {
            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound();

            ent.Status = dto.Status;
            ent.Reason = dto.Reason;
            await repo.UpdateAsync(ent);

            return Results.Ok(ToDto(ent));
        }
    }
}

