// CompGateApi.Endpoints/CblRequestEndpoints.cs
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
    public class CblRequestEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            // ── COMPANY ROUTES ────────────────────────────────────────────────
            var company = app
                .MapGroup("/api/cblrequests")
                .WithTags("CblRequests")
                .RequireAuthorization("RequireCompanyUser")
                .RequireAuthorization("CanRequestCbl");

            company.MapGet("/", GetMyRequests)
                   .Produces<PagedResult<CblRequestDto>>(200);

            company.MapGet("/{id:int}", GetMyRequestById)
                   .Produces<CblRequestDto>(200)
                   .Produces(404);

            company.MapPost("/", CreateMyRequest)
                   .Accepts<CblRequestCreateDto>("application/json")
                   .Produces<CblRequestDto>(201)
                   .Produces(400);

            company.MapPut("/{id:int}", UpdateMyRequest)
                  .Accepts<CblRequestCreateDto>("application/json")
                  .Produces<CblRequestDto>(200)
                  .Produces(400)
                  .Produces(404);


            // ── ADMIN ROUTES ──────────────────────────────────────────────────
            var admin = app
                .MapGroup("/api/admin/cblrequests")
                .WithTags("CblRequests")
                .RequireAuthorization("RequireAdminUser")
                .RequireAuthorization("AdminAccess");

            admin.MapGet("/", GetAllAdmin)
                 .Produces<PagedResult<CblRequestDto>>(200);

            admin.MapPut("/{id:int}/status", UpdateStatus)
                 .Accepts<CblRequestStatusUpdateDto>("application/json")
                 .Produces<CblRequestDto>(200)
                 .Produces(404);

            admin.MapGet("/{id:int}", AdminGetById)
                .WithName("AdminGetCblRequestById")
                .Produces<CblRequestDto>(200)
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
            ICblRequestRepository repo,
            IUserRepository userRepo,
            ILogger<CblRequestEndpoints> log,
            int page = 1,
            int limit = 50,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? searchBy = null)
        {
            log.LogInformation("GetMyRequests: Authenticated={IsAuth}, Claims={Claims}",
                ctx.User.Identity?.IsAuthenticated == true,
                string.Join(";", ctx.User.Claims.Select(c => $"{c.Type}={c.Value}")));

            try
            {
                var authId = GetAuthUserId(ctx);
                log.LogDebug("Parsed AuthUserId={AuthId}", authId);

                var token = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, token);
                if (me == null)
                {
                    log.LogWarning("User mapping not found: AuthId={AuthId}", authId);
                    return Results.Unauthorized();
                }

                if (!me.CompanyId.HasValue)
                    return Results.Unauthorized();
                var cid = me.CompanyId.Value;
                var list = await repo.GetAllByCompanyAsync(cid, searchTerm, searchBy, page, limit);
                var total = await repo.GetCountByCompanyAsync(cid, searchTerm, searchBy);

                var dtos = list.Select(r => new CblRequestDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    PartyName = r.PartyName,
                    Capital = r.Capital,
                    FoundingDate = r.FoundingDate,
                    LegalForm = r.LegalForm,
                    BranchOrAgency = r.BranchOrAgency,
                    CurrentAccount = r.CurrentAccount,
                    AccountOpening = r.AccountOpening,
                    CommercialLicense = r.CommercialLicense,
                    ValidatyLicense = r.ValidatyLicense,
                    CommercialRegistration = r.CommercialRegistration,
                    ValidatyRegister = r.ValidatyRegister,
                    StatisticalCode = r.StatisticalCode,
                    ValidatyCode = r.ValidatyCode,
                    ChamberNumber = r.ChamberNumber,
                    ValidatyChamber = r.ValidatyChamber,
                    TaxNumber = r.TaxNumber,
                    Office = r.Office,
                    LegalRepresentative = r.LegalRepresentative,
                    RepresentativeNumber = r.RepresentativeNumber,
                    BirthDate = r.BirthDate,
                    PassportNumber = r.PassportNumber,
                    PassportIssuance = r.PassportIssuance,
                    PassportExpiry = r.PassportExpiry,
                    Mobile = r.Mobile,
                    Address = r.Address,
                    PackingDate = r.PackingDate,
                    SpecialistName = r.SpecialistName,
                    Status = r.Status,
                    Officials = r.Officials.Select(o => new CblRequestOfficialDto
                    { Id = o.Id, Name = o.Name, Position = o.Position }).ToList(),
                    Signatures = r.Signatures.Select(s => new CblRequestSignatureDto
                    { Id = s.Id, Name = s.Name, Signature = s.Signature, Status = s.Status }).ToList(),
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                }).ToList();

                return Results.Ok(new PagedResult<CblRequestDto>
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


        public static async Task<IResult> UpdateMyRequest(
            int id,
            [FromBody] CblRequestCreateDto dto,
            HttpContext ctx,
            ICblRequestRepository repo,
            IUserRepository userRepo,
            IValidator<CblRequestCreateDto> validator,
            ILogger<CblRequestEndpoints> log)
        {
            log.LogInformation("UpdateMyRequest payload: {@Dto}", dto);
            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                log.LogWarning("Validation failed: {Errors}", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));
            }

            try
            {
                var authId = GetAuthUserId(ctx);
                var token = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? string.Empty;
                var me = await userRepo.GetUserByAuthId(authId, token);
                if (me == null || !me.CompanyId.HasValue)
                    return Results.Unauthorized();

                var ent = await repo.GetByIdAsync(id);
                if (ent == null || ent.CompanyId != me.CompanyId.Value)
                    return Results.NotFound();

                if (ent.Status.Equals("printed", StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest("Cannot edit a printed form.");

                // update fields
                ent.PartyName = dto.PartyName;
                ent.Capital = dto.Capital;
                ent.FoundingDate = dto.FoundingDate;
                ent.LegalForm = dto.LegalForm;
                ent.BranchOrAgency = dto.BranchOrAgency;
                ent.CurrentAccount = dto.CurrentAccount;
                ent.AccountOpening = dto.AccountOpening;
                ent.CommercialLicense = dto.CommercialLicense;
                ent.ValidatyLicense = dto.ValidatyLicense;
                ent.CommercialRegistration = dto.CommercialRegistration;
                ent.ValidatyRegister = dto.ValidatyRegister;
                ent.StatisticalCode = dto.StatisticalCode;
                ent.ValidatyCode = dto.ValidatyCode;
                ent.ChamberNumber = dto.ChamberNumber;
                ent.ValidatyChamber = dto.ValidatyChamber;
                ent.TaxNumber = dto.TaxNumber;
                ent.Office = dto.Office;
                ent.LegalRepresentative = dto.LegalRepresentative;
                ent.RepresentativeNumber = dto.RepresentativeNumber;
                ent.BirthDate = dto.BirthDate;
                ent.PassportNumber = dto.PassportNumber;
                ent.PassportIssuance = dto.PassportIssuance;
                ent.PassportExpiry = dto.PassportExpiry;
                ent.Mobile = dto.Mobile;
                ent.Address = dto.Address;
                ent.PackingDate = dto.PackingDate;
                ent.SpecialistName = dto.SpecialistName;

                ent.Officials = dto.Officials
                    .Select(o => new CblRequestOfficial { Name = o.Name, Position = o.Position })
                    .ToList();
                ent.Signatures = dto.Signatures
                    .Select(s => new CblRequestSignature { Name = s.Name, Signature = s.Signature })
                    .ToList();

                await repo.UpdateAsync(ent);
                log.LogInformation("Updated CblRequest Id={RequestId}", id);

                var outDto = new CblRequestDto
                {
                    Id = ent.Id,
                    UserId = ent.UserId,
                    PartyName = ent.PartyName,
                    Capital = ent.Capital,
                    FoundingDate = ent.FoundingDate,
                    LegalForm = ent.LegalForm,
                    BranchOrAgency = ent.BranchOrAgency,
                    CurrentAccount = ent.CurrentAccount,
                    AccountOpening = ent.AccountOpening,
                    CommercialLicense = ent.CommercialLicense,
                    ValidatyLicense = ent.ValidatyLicense,
                    CommercialRegistration = ent.CommercialRegistration,
                    ValidatyRegister = ent.ValidatyRegister,
                    StatisticalCode = ent.StatisticalCode,
                    ValidatyCode = ent.ValidatyCode,
                    ChamberNumber = ent.ChamberNumber,
                    ValidatyChamber = ent.ValidatyChamber,
                    TaxNumber = ent.TaxNumber,
                    Office = ent.Office,
                    LegalRepresentative = ent.LegalRepresentative,
                    RepresentativeNumber = ent.RepresentativeNumber,
                    BirthDate = ent.BirthDate,
                    PassportNumber = ent.PassportNumber,
                    PassportIssuance = ent.PassportIssuance,
                    PassportExpiry = ent.PassportExpiry,
                    Mobile = ent.Mobile,
                    Address = ent.Address,
                    PackingDate = ent.PackingDate,
                    SpecialistName = ent.SpecialistName,
                    Status = ent.Status,
                    Officials = ent.Officials
                        .Select(o => new CblRequestOfficialDto { Id = o.Id, Name = o.Name, Position = o.Position })
                        .ToList(),
                    Signatures = ent.Signatures
                        .Select(s => new CblRequestSignatureDto { Id = s.Id, Name = s.Name, Signature = s.Signature, Status = s.Status })
                        .ToList(),
                    CreatedAt = ent.CreatedAt,
                    UpdatedAt = ent.UpdatedAt
                };

                return Results.Ok(outDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Auth error in UpdateMyRequest");
                return Results.Unauthorized();
            }
        }

        public static async Task<IResult> GetMyRequestById(
            int id,
            HttpContext ctx,
            ICblRequestRepository repo,
            IUserRepository userRepo,
            ILogger<CblRequestEndpoints> log)
        {
            log.LogInformation("GetMyRequestById({Id})", id);
            try
            {
                var authId = GetAuthUserId(ctx);
                var token = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, token);
                if (me == null) return Results.Unauthorized();

                var ent = await repo.GetByIdAsync(id);
                if (ent == null
                    || !me.CompanyId.HasValue
                    || ent.CompanyId != me.CompanyId.Value)
                    return Results.NotFound("Not found");

                var dto = new CblRequestDto
                {
                    Id = ent.Id,
                    UserId = ent.UserId,
                    PartyName = ent.PartyName,
                    Capital = ent.Capital,
                    FoundingDate = ent.FoundingDate,
                    LegalForm = ent.LegalForm,
                    BranchOrAgency = ent.BranchOrAgency,
                    CurrentAccount = ent.CurrentAccount,
                    AccountOpening = ent.AccountOpening,
                    CommercialLicense = ent.CommercialLicense,
                    ValidatyLicense = ent.ValidatyLicense,
                    CommercialRegistration = ent.CommercialRegistration,
                    ValidatyRegister = ent.ValidatyRegister,
                    StatisticalCode = ent.StatisticalCode,
                    ValidatyCode = ent.ValidatyCode,
                    ChamberNumber = ent.ChamberNumber,
                    ValidatyChamber = ent.ValidatyChamber,
                    TaxNumber = ent.TaxNumber,
                    Office = ent.Office,
                    LegalRepresentative = ent.LegalRepresentative,
                    RepresentativeNumber = ent.RepresentativeNumber,
                    BirthDate = ent.BirthDate,
                    PassportNumber = ent.PassportNumber,
                    PassportIssuance = ent.PassportIssuance,
                    PassportExpiry = ent.PassportExpiry,
                    Mobile = ent.Mobile,
                    Address = ent.Address,
                    PackingDate = ent.PackingDate,
                    SpecialistName = ent.SpecialistName,
                    Status = ent.Status,
                    Officials = ent.Officials.Select(o => new CblRequestOfficialDto { Id = o.Id, Name = o.Name, Position = o.Position }).ToList(),
                    Signatures = ent.Signatures.Select(s => new CblRequestSignatureDto { Id = s.Id, Name = s.Name, Signature = s.Signature, Status = s.Status }).ToList(),
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

        public static async Task<IResult> CreateMyRequest(
            [FromBody] CblRequestCreateDto dto,
            HttpContext ctx,
            ICblRequestRepository repo,
            IUserRepository userRepo,
            IValidator<CblRequestCreateDto> validator,
            ILogger<CblRequestEndpoints> log)
        {
            log.LogInformation("CreateMyRequest payload: {@Dto}", dto);
            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                log.LogWarning("Validation failed: {Errors}", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));
            }

            try
            {
                var authId = GetAuthUserId(ctx);
                log.LogDebug("Parsed AuthUserId={AuthId}", authId);

                var token = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, token);
                if (me == null) return Results.Unauthorized();

                if (!me.CompanyId.HasValue)
                    return Results.Unauthorized();

                var ent = new CblRequest
                {
                    UserId = me.UserId,
                    CompanyId = me.CompanyId.Value,
                    PartyName = dto.PartyName,
                    Capital = dto.Capital,
                    FoundingDate = dto.FoundingDate,
                    LegalForm = dto.LegalForm,
                    BranchOrAgency = dto.BranchOrAgency,
                    CurrentAccount = dto.CurrentAccount,
                    AccountOpening = dto.AccountOpening,
                    CommercialLicense = dto.CommercialLicense,
                    ValidatyLicense = dto.ValidatyLicense,
                    CommercialRegistration = dto.CommercialRegistration,
                    ValidatyRegister = dto.ValidatyRegister,
                    StatisticalCode = dto.StatisticalCode,
                    ValidatyCode = dto.ValidatyCode,
                    ChamberNumber = dto.ChamberNumber,
                    ValidatyChamber = dto.ValidatyChamber,
                    TaxNumber = dto.TaxNumber,
                    Office = dto.Office,
                    LegalRepresentative = dto.LegalRepresentative,
                    RepresentativeNumber = dto.RepresentativeNumber,
                    BirthDate = dto.BirthDate,
                    PassportNumber = dto.PassportNumber,
                    PassportIssuance = dto.PassportIssuance,
                    PassportExpiry = dto.PassportExpiry,
                    Mobile = dto.Mobile,
                    Address = dto.Address,
                    PackingDate = dto.PackingDate,
                    SpecialistName = dto.SpecialistName,
                    Status = "Pending",
                    Officials = dto.Officials.Select(o => new CblRequestOfficial { Name = o.Name, Position = o.Position }).ToList(),
                    Signatures = dto.Signatures.Select(s => new CblRequestSignature { Name = s.Name, Signature = s.Signature }).ToList()
                };
                await repo.CreateAsync(ent);
                log.LogInformation("Created CblRequest Id={RequestId}", ent.Id);

                var outDto = new CblRequestDto
                {
                    Id = ent.Id,
                    UserId = ent.UserId,
                    PartyName = ent.PartyName,
                    Capital = ent.Capital,
                    FoundingDate = ent.FoundingDate,
                    LegalForm = ent.LegalForm,
                    BranchOrAgency = ent.BranchOrAgency,
                    CurrentAccount = ent.CurrentAccount,
                    AccountOpening = ent.AccountOpening,
                    CommercialLicense = ent.CommercialLicense,
                    ValidatyLicense = ent.ValidatyLicense,
                    CommercialRegistration = ent.CommercialRegistration,
                    ValidatyRegister = ent.ValidatyRegister,
                    StatisticalCode = ent.StatisticalCode,
                    ValidatyCode = ent.ValidatyCode,
                    ChamberNumber = ent.ChamberNumber,
                    ValidatyChamber = ent.ValidatyChamber,
                    TaxNumber = ent.TaxNumber,
                    Office = ent.Office,
                    LegalRepresentative = ent.LegalRepresentative,
                    RepresentativeNumber = ent.RepresentativeNumber,
                    BirthDate = ent.BirthDate,
                    PassportNumber = ent.PassportNumber,
                    PassportIssuance = ent.PassportIssuance,
                    PassportExpiry = ent.PassportExpiry,
                    Mobile = ent.Mobile,
                    Address = ent.Address,
                    PackingDate = ent.PackingDate,
                    SpecialistName = ent.SpecialistName,
                    Status = ent.Status,
                    Officials = ent.Officials.Select(o => new CblRequestOfficialDto { Id = o.Id, Name = o.Name, Position = o.Position }).ToList(),
                    Signatures = ent.Signatures.Select(s => new CblRequestSignatureDto { Id = s.Id, Name = s.Name, Signature = s.Signature, Status = s.Status }).ToList(),
                    CreatedAt = ent.CreatedAt,
                    UpdatedAt = ent.UpdatedAt
                };
                return Results.Created($"/api/cblrequests/{ent.Id}", outDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Auth error in CreateMyRequest");
                return Results.Unauthorized();
            }
        }

        public static async Task<IResult> GetAllAdmin(
            ICblRequestRepository repo,
            ILogger<CblRequestEndpoints> log,
            int page = 1,
            int limit = 50,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? searchBy = null)
        {
            log.LogInformation("Admin:GetAll called (search={SearchBy}='{SearchTerm}', page={Page}, limit={Limit})",
                searchBy, searchTerm, page, limit);

            var list = await repo.GetAllAsync(searchTerm, searchBy, page, limit);
            var total = await repo.GetCountAsync(searchTerm, searchBy);

            var dtos = list.Select(r => new CblRequestDto
            {
                Id = r.Id,
                UserId = r.UserId,
                PartyName = r.PartyName,
                Capital = r.Capital,
                FoundingDate = r.FoundingDate,
                LegalForm = r.LegalForm,
                BranchOrAgency = r.BranchOrAgency,
                CurrentAccount = r.CurrentAccount,
                AccountOpening = r.AccountOpening,
                CommercialLicense = r.CommercialLicense,
                ValidatyLicense = r.ValidatyLicense,
                CommercialRegistration = r.CommercialRegistration,
                ValidatyRegister = r.ValidatyRegister,
                StatisticalCode = r.StatisticalCode,
                ValidatyCode = r.ValidatyCode,
                ChamberNumber = r.ChamberNumber,
                ValidatyChamber = r.ValidatyChamber,
                TaxNumber = r.TaxNumber,
                Office = r.Office,
                LegalRepresentative = r.LegalRepresentative,
                RepresentativeNumber = r.RepresentativeNumber,
                BirthDate = r.BirthDate,
                PassportNumber = r.PassportNumber,
                PassportIssuance = r.PassportIssuance,
                PassportExpiry = r.PassportExpiry,
                Mobile = r.Mobile,
                Address = r.Address,
                PackingDate = r.PackingDate,
                SpecialistName = r.SpecialistName,
                Status = r.Status,
                Officials = r.Officials.Select(o => new CblRequestOfficialDto { Id = o.Id, Name = o.Name, Position = o.Position }).ToList(),
                Signatures = r.Signatures.Select(s => new CblRequestSignatureDto { Id = s.Id, Name = s.Name, Signature = s.Signature, Status = s.Status }).ToList(),
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            return Results.Ok(new PagedResult<CblRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalPages = (int)Math.Ceiling(total / (double)limit),
                TotalRecords = total
            });
        }

        public static async Task<IResult> UpdateStatus(
            int id,
            CblRequestStatusUpdateDto dto,
            ICblRequestRepository repo,
            IValidator<CblRequestStatusUpdateDto> validator,
            ILogger<CblRequestEndpoints> log)
        {
            log.LogInformation("Admin:UpdateStatus id={Id} → Status={Status}", id, dto.Status);
            var results = await validator.ValidateAsync(dto);
            if (!results.IsValid)
            {
                log.LogWarning("Status validation errors: {Errors}", string.Join("; ", results.Errors.Select(e => e.ErrorMessage)));
                return Results.BadRequest(results.Errors.Select(e => e.ErrorMessage));
            }

            var ent = await repo.GetByIdAsync(id);
            if (ent == null) return Results.NotFound("Not found");

            ent.Status = dto.Status;
            await repo.UpdateAsync(ent);
            log.LogInformation("Updated CblRequest {Id} → Status='{Status}'", id, dto.Status);

            // return updated DTO
            return Results.Ok(new CblRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                PartyName = ent.PartyName,
                // … copy other fields …
                Status = ent.Status,
                Officials = ent.Officials.Select(o => new CblRequestOfficialDto { Id = o.Id, Name = o.Name, Position = o.Position }).ToList(),
                Signatures = ent.Signatures.Select(s => new CblRequestSignatureDto { Id = s.Id, Name = s.Name, Signature = s.Signature, Status = s.Status }).ToList(),
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            });
        }

        public static async Task<IResult> AdminGetById(
    int id,
    [FromServices] ICblRequestRepository repo,
    [FromServices] ILogger<CblRequestEndpoints> log)
        {
            log.LogInformation("Admin:GetById CblRequest {Id}", id);

            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound("CBL request not found.");

            var dto = new CblRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                PartyName = ent.PartyName,
                Capital = ent.Capital,
                FoundingDate = ent.FoundingDate,
                LegalForm = ent.LegalForm,
                BranchOrAgency = ent.BranchOrAgency,
                CurrentAccount = ent.CurrentAccount,
                AccountOpening = ent.AccountOpening,
                CommercialLicense = ent.CommercialLicense,
                ValidatyLicense = ent.ValidatyLicense,
                CommercialRegistration = ent.CommercialRegistration,
                ValidatyRegister = ent.ValidatyRegister,
                StatisticalCode = ent.StatisticalCode,
                ValidatyCode = ent.ValidatyCode,
                ChamberNumber = ent.ChamberNumber,
                ValidatyChamber = ent.ValidatyChamber,
                TaxNumber = ent.TaxNumber,
                Office = ent.Office,
                LegalRepresentative = ent.LegalRepresentative,
                RepresentativeNumber = ent.RepresentativeNumber,
                BirthDate = ent.BirthDate,
                PassportNumber = ent.PassportNumber,
                PassportIssuance = ent.PassportIssuance,
                PassportExpiry = ent.PassportExpiry,
                Mobile = ent.Mobile,
                Address = ent.Address,
                PackingDate = ent.PackingDate,
                SpecialistName = ent.SpecialistName,
                Status = ent.Status,
                Officials = ent.Officials
                              .Select(o => new CblRequestOfficialDto { Id = o.Id, Name = o.Name, Position = o.Position })
                              .ToList(),
                Signatures = ent.Signatures
                               .Select(s => new CblRequestSignatureDto { Id = s.Id, Name = s.Name, Signature = s.Signature, Status = s.Status })
                               .ToList(),
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Ok(dto);
        }
    }
}
