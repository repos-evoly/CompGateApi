// CompGateApi.Endpoints/CompanyEndpoints.cs
using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using CompGateApi.Data.Context;

namespace CompGateApi.Endpoints
{
    public class CompanyEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var companies = app
                .MapGroup("/api/companies")
                .WithTags("Companies");

            // 1️⃣ Public KYC lookup (no auth)
            companies.MapGet("/kyc/{code}", LookupKyc)
                     .WithName("LookupKyc")
                     .Produces(200)
                     .Produces(400);

            // 2️⃣ Registration of company‐admin user (no auth)
            companies.MapPost("/register", RegisterCompany)
                     .WithName("RegisterCompany")
                     .Accepts<CompanyRegistrationDto>("application/json")
                     .Produces(201)
                     .Produces(400);

            companies.MapGet("/getInfo/{code}", GetCompanyInfo)
                    .WithName("GetCompanyInfo")
                    .Produces(200)
                    .Produces(400);
            companies.MapPut("/public/users/{userId}", PublicEditUser)
                        .WithName("PublicEditUser")
                        .Accepts<PublicEditUserDto>("application/json")
                        .Produces(StatusCodes.Status204NoContent)
                        .Produces(StatusCodes.Status404NotFound);

            // ── ADMIN portal ───────────────────────────────────────────────
            var admin = companies
                .MapGroup("/admin")
                .RequireAuthorization("RequireAdminUser")
                .RequireAuthorization("AdminAccess");

            // 3a) List & filter companies
            admin.MapGet("/", GetAllCompanies)
                 .WithName("GetAllCompanies")
                 .Produces<PagedResult<CompanyListDto>>(200);

            // 3b) Approve/Reject KYC
            admin.MapPut("/{code}/status", UpdateCompanyStatus)
                 .WithName("UpdateCompanyStatus")
                 .Accepts<CompanyStatusUpdateDto>("application/json")
                 .Produces(200)
                 .Produces(404);
            admin.MapGet("/{code}", GetCompanyByCode)
            .WithName("GetCompanyByCode")
            .Produces<CompanyListDto>(200)
            .Produces(404);

            // ── COMPANY-ADMIN portal ────────────────────────────────────
            var companyAdmin = companies
                .MapGroup("/{code}/users")
                .RequireAuthorization("RequireCompanyUser")
                .RequireAuthorization("RequireCompanyAdmin")
                .WithTags("CompanyUsers");

            // 4a) Add employee
            companyAdmin.MapPost("/", AddCompanyUser)
                        .WithName("AddCompanyUser")
                        .Accepts<CompanyEmployeeRegistrationDto>("application/json")
                        .Produces<CompanyEmployeeDetailsDto>(201)
                        .Produces(400)
                        .Produces(401);

            // 4b) List employees
            companyAdmin.MapGet("/", GetCompanyUsers)
                        .WithName("GetCompanyUsers")
                        .Produces<List<CompanyEmployeeDetailsDto>>(200);

            companyAdmin
                .MapGet("/{userId:int}", GetCompanyUserById)
                .WithName("GetCompanyUserById")
                .Produces<CompanyEmployeeDetailsDto>(200)
                .Produces(401)
                .Produces(404);

            companyAdmin
              .MapPut("/{userId:int}", EditCompanyUser)
              .WithName("EditCompanyUser")
              .Accepts<EditUserDto>("application/json")
              .Produces(204)
              .Produces(400)
              .Produces(401)
              .Produces(404);
        }

        static int ResolveAuthUserId(HttpContext ctx)
        {
            var raw = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? ctx.User.FindFirst("nameid")?.Value;
            if (!int.TryParse(raw, out var id))
                throw new UnauthorizedAccessException("Missing or invalid 'nameid' claim.");
            return id;
        }

        public static async Task<IResult> GetCompanyInfo(
          [FromRoute] string code,
          HttpContext ctx,
          [FromServices] ICompanyRepository companyRepo,
          [FromServices] IAttachmentRepository attachmentRepo,
          [FromServices] IUserRepository userRepo,
          [FromServices] ILogger<CompanyEndpoints> log)
        {
            log.LogInformation("→ GetCompanyInfo {Code}", code);

            // 1) Load company
            var company = await companyRepo.GetByCodeAsync(code);
            if (company == null)
            {
                log.LogWarning("  Company '{Code}' not found", code);
                return Results.NotFound($"Company '{code}' not found.");
            }

            // 2) Eager‐load attachments via the attachment repository (because
            //    we don’t assume company.Attachments was Include()‐ed).
            var rawAttachments = await attachmentRepo.GetByCompany(company.Id);

            // 3) Find the single “company‐admin” user for this company.
            //    We call GetUsersAsync(...) with hasCompany=true to get all users who have a non‐null CompanyId.
            //    Then filter by CompanyId and IsCompanyAdmin == true.
            //
            //    NOTE: your IUserRepository.GetUsersAsync(...) must return a DTO that has at least:
            //          { UserId, CompanyId, FirstName, LastName, Phone, IsCompanyAdmin }.
            //          If it doesn’t, you’ll need to add IsCompanyAdmin to your UserDetailsDto.
            //
            var token = ctx.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var adminCandidates = await userRepo.GetUsersAsync(
                searchTerm: null,
                searchBy: null,
                hasCompany: true,
                roleId: null,
                page: 1,
                limit: int.MaxValue,
                authToken: token
            );

            var adminUser = adminCandidates
                .SingleOrDefault(u => u.CompanyId == company.Id && u.IsCompanyAdmin);

            // 4) If no admin has been set yet, we still return the company data + attachments,
            //    but we leave admin‐info = null.
            string? adminFirstName = null;
            string? adminLastName = null;
            string? adminPhone = null;

            if (adminUser != null)
            {
                adminFirstName = adminUser.FirstName;
                adminLastName = adminUser.LastName;
                adminPhone = adminUser.Phone;
            }
            else
            {
                log.LogWarning("  No company‐admin found for company {CompanyId}", company.Id);
            }

            // 5) Project into a response shape. You can either: 
            //    • Return a strongly‐typed DTO type (e.g. “GetCompanyInfoDto”), or 
            //    • Return an anonymous object as below.
            //    In this example we'll return an anonymous JSON object:
            var result = new
            {
                code = company.Code,
                name = company.Name,
                isActive = company.IsActive,
                registrationStatus = company.RegistrationStatus,
                registrationStatusMessage = company.RegistrationStatusMessage,
                kycRequestedAt = company.KycRequestedAt,
                kycReviewedAt = company.KycReviewedAt,
                kycBranchId = company.KycBranchId,
                kycLegalCompanyName = company.KycLegalCompanyName,
                kycLegalCompanyNameLt = company.KycLegalCompanyNameLt,
                kycMobile = company.KycMobile,
                kycNationality = company.KycNationality,
                kycCity = company.KycCity,

                // ■ attachments (map to your AttachmentDto)
                attachments = rawAttachments
                    .Select(a => new AttachmentDto
                    {
                        Id = a.Id,
                        AttSubject = a.AttSubject,
                        AttFileName = a.AttFileName,
                        AttOriginalFileName = a.AttOriginalFileName,
                        AttMime = a.AttMime,
                        AttSize = a.AttSize,
                        AttUrl = a.AttUrl,
                        Description = a.Description,
                        CreatedBy = a.CreatedBy,
                        CreatedAt = a.CreatedAt,
                        UpdatedAt = a.UpdatedAt
                    })
                    .ToList(),

                // ■ company‐admin contact info (will be null if no adminUser found)
                adminContact = adminUser is null
                    ? null
                    : new
                    {
                        firstName = adminFirstName,
                        lastName = adminLastName,
                        phone = adminPhone
                    }
            };

            log.LogInformation("← GetCompanyInfo {Code} returning result", code);
            return Results.Ok(result);
        }


        public static async Task<IResult> PublicEditUser(
        int userId,
        [FromBody] PublicEditUserDto dto,
        [FromServices] CompGateApiDbContext db
    )
        {
            // 1) load the user from the EF‐Core DbContext
            var user = await db.Users.FindAsync(userId);
            if (user == null)
            {
                return Results.NotFound($"User '{userId}' not found.");
            }

            // 2) update only those three fields
            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.Phone = dto.Phone;

            // 3) save and return 204 No Content
            await db.SaveChangesAsync();
            return Results.NoContent();
        }
        public static async Task<IResult> LookupKyc(
            string code,
            [FromServices] ICompanyRepository repo)
        {

            // 0️⃣ Prevent lookup for codes already in our DB
            var existing = await repo.GetByCodeAsync(code);
            if (existing != null)
            {
                // you can choose BadRequest(400) or Conflict(409)
                return Results.BadRequest(new { error = "Company code already registered." });
            }

            if (code.Length != 6)
                return Results.BadRequest("Company code must be exactly 6 characters.");

            var kyc = await repo.LookupKycAsync(code);
            if (kyc == null || string.IsNullOrWhiteSpace(kyc.companyId))
                return Results.Ok(new { hasKyc = false });

            return Results.Ok(new { hasKyc = true, data = kyc });
        }

        public static async Task<IResult> RegisterCompany(
            [FromBody] CompanyRegistrationDto dto,
            [FromServices] ICompanyRepository repo,
            [FromServices] ILogger<CompanyEndpoints> log)
        {
            // Make sure KYC was done first
            if (!await repo.CanRegisterCompanyAsync(dto.CompanyCode))
                return Results.BadRequest(new { error = "You must perform a successful KYC lookup before registering." });

            // Attempt registration
            var result = await repo.RegisterCompanyAdminAsync(dto);

            if (!result.Success)
            {
                log.LogWarning("Company registration failed: {Error}", result.Error);
                return Results.BadRequest(new { error = result.Error });
            }

            // Success → 201 + Location header
            return Results.Ok(new
            {
                success = true,
                companyCode = dto.CompanyCode,
                location = result.Location,
                message = "Registration successful, pending admin approval."
            });
        }


        public static async Task<IResult> GetAllCompanies(
            [FromServices] ICompanyRepository repo,
            [FromServices] ILogger<CompanyEndpoints> log,
            [FromQuery] string? searchTerm = null,
            [FromQuery] RegistrationStatus? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            log.LogInformation(
                "Admin:GetAllCompanies(search={Search},status={Status},page={Page},limit={Limit})",
                searchTerm, status, page, limit);

            var items = await repo.GetAllCompaniesAsync(searchTerm, status, page, limit);
            var total = await repo.GetCompaniesCountAsync(searchTerm, status);

            return Results.Ok(new PagedResult<CompanyListDto>
            {
                Data = items,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit)
            });
        }

        public static async Task<IResult> UpdateCompanyStatus(
            string code,
            [FromBody] CompanyStatusUpdateDto dto,
            [FromServices] ICompanyRepository repo)
        {
            var ok = await repo.UpdateCompanyStatusAsync(code, dto);
            return ok ? Results.Ok() : Results.NotFound();
        }

        public static async Task<IResult> AddCompanyUser(
            string code,
            [FromBody] CompanyEmployeeRegistrationDto dto,
            HttpContext ctx,
            [FromServices] IUserRepository userRepo,
            [FromServices] ICompanyRepository companyRepo,
            [FromServices] IValidator<CompanyEmployeeRegistrationDto> validator,
            [FromServices] IHttpClientFactory httpFactory,
            [FromServices] ILogger<CompanyEndpoints> log)
        {
            var vr = await validator.ValidateAsync(dto);
            if (!vr.IsValid)
                return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));

            int callerAuth;
            try { callerAuth = ResolveAuthUserId(ctx); }
            catch { return Results.Unauthorized(); }

            var bearer = ctx.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var me = await userRepo.GetUserByAuthId(callerAuth, bearer);
            if (me == null) return Results.Unauthorized();

            var company = await companyRepo.GetByCodeAsync(code);
            if (company == null) return Results.NotFound($"Company '{code}' not found.");
            if (!me.IsCompanyAdmin || !string.Equals(me.CompanyCode, company.Code, StringComparison.OrdinalIgnoreCase))
                return Results.Unauthorized();

            var client = httpFactory.CreateClient("AuthApi");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            var authPayload = new
            {
                username = dto.Username,
                fullNameLT = dto.FirstName,
                fullNameAR = dto.LastName,
                email = dto.Email,
                password = dto.Password,
                roleId = dto.RoleId
            };
            var resp = await client.PostAsJsonAsync("api/auth/register", authPayload);
            if (!resp.IsSuccessStatusCode)
                return Results.BadRequest("External auth registration failed.");

            var regText = await resp.Content.ReadAsStringAsync();
            var reg = JsonSerializer.Deserialize<AuthRegisterResponseDto>(regText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            if (reg.userId <= 0) return Results.BadRequest("Auth returned invalid userId.");

            var user = new User
            {
                AuthUserId = reg.userId,
                CompanyId = company.Id,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Phone = dto.Phone,
                RoleId = dto.RoleId,
                IsCompanyAdmin = false,
                // ServicePackageId = me.ServicePackageId
            };
            await userRepo.AddUser(user);

            var perms = (await userRepo.GetUserPermissions(user.Id))
                         .Where(p => p.HasPermission == 1)
                         .Select(p => p.PermissionName)
                         .ToList();

            var outDto = new CompanyEmployeeDetailsDto
            {
                Id = user.Id,
                AuthUserId = user.AuthUserId,
                CompanyCode = company.Code,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                RoleId = user.RoleId,
                Permissions = perms
            };

            return Results.Created($"/api/companies/{code}/users/{user.Id}", outDto);
        }

        public static async Task<IResult> GetCompanyUsers(
            string code,
            HttpContext ctx,
            [FromServices] IUserRepository userRepo,
            [FromServices] ICompanyRepository companyRepo)
        {
            int authId = ResolveAuthUserId(ctx);
            var token = ctx.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var me = await userRepo.GetUserByAuthId(authId, token);
            if (me == null) return Results.Unauthorized();

            var company = await companyRepo.GetByCodeAsync(code);
            if (company == null) return Results.NotFound($"Company '{code}' not found.");
            if (me == null || !string.Equals(me.CompanyCode, code, StringComparison.OrdinalIgnoreCase) || !me.IsCompanyAdmin)
                return Results.Unauthorized();

            var all = await userRepo.GetUsersAsync(
                searchTerm: null,
                searchBy: null,
                hasCompany: true,
                roleId: null,
                page: 1,
                limit: int.MaxValue,
                authToken: token
            );
            var ours = all
               .Where(u => u.CompanyId == company.Id && u.AuthUserId != me.AuthUserId)
                .Select(u => new CompanyEmployeeDetailsDto
                {
                    Id = u.UserId,
                    AuthUserId = u.AuthUserId,
                    CompanyCode = company.Code,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Phone = u.Phone,
                    RoleId = u.RoleId,
                    Permissions = u.Permissions
                })
                .ToList();

            return Results.Ok(ours);
        }

        public static async Task<IResult> GetCompanyByCode(
    string code,
    [FromServices] ICompanyRepository repo)
        {
            var c = await repo.GetByCodeAsync(code);
            if (c == null)
                return Results.NotFound($"Company '{code}' not found.");

            var dto = new CompanyListDto
            {
                Code = c.Code,
                Name = c.Name,
                IsActive = c.IsActive,
                RegistrationStatus = c.RegistrationStatus,
                RegistrationStatusMessage = c.RegistrationStatusMessage,
                KycRequestedAt = c.KycRequestedAt,
                KycReviewedAt = c.KycReviewedAt,
                KycBranchId = c.KycBranchId,
                KycLegalCompanyName = c.KycLegalCompanyName,
                KycLegalCompanyNameLt = c.KycLegalCompanyNameLt,
                KycMobile = c.KycMobile,
                KycNationality = c.KycNationality,
                KycCity = c.KycCity,
                Attachments = c.Attachments
                                         .Select(a => new AttachmentDto
                                         {
                                             Id = a.Id,
                                             AttSubject = a.AttSubject,
                                             AttFileName = a.AttFileName,
                                             AttOriginalFileName = a.AttOriginalFileName,
                                             AttMime = a.AttMime,
                                             AttSize = a.AttSize,
                                             AttUrl = a.AttUrl,
                                             Description = a.Description,
                                             CreatedBy = a.CreatedBy,
                                             CreatedAt = a.CreatedAt,
                                             UpdatedAt = a.UpdatedAt
                                         })
                                         .ToList()
            };

            return Results.Ok(dto);
        }

        public static async Task<IResult> GetCompanyUserById(
       string code,
       int userId,
       HttpContext ctx,
       [FromServices] IUserRepository userRepo,
       [FromServices] ICompanyRepository companyRepo,
       [FromServices] ILogger<CompanyEndpoints> log)
        {
            log.LogInformation("→ GetCompanyUserById {Code}/{UserId}", code, userId);

            // 1️⃣ Authenticate
            int callerAuth;
            try { callerAuth = ResolveAuthUserId(ctx); }
            catch
            {
                log.LogWarning("  Missing/invalid nameid claim");
                return Results.Unauthorized();
            }
            var token = ctx.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var me = await userRepo.GetUserByAuthId(callerAuth, token);
            if (me == null)
            {
                log.LogWarning("  No local user for authId={Auth}", callerAuth);
                return Results.Unauthorized();
            }

            // 2️⃣ Company exists + admin?
            var company = await companyRepo.GetByCodeAsync(code);
            if (company == null)
            {
                log.LogWarning("  Company '{Code}' not found", code);
                return Results.NotFound($"Company '{code}' not found.");
            }
            if (!me.IsCompanyAdmin || !string.Equals(me.CompanyCode, code, StringComparison.OrdinalIgnoreCase))
            {
                log.LogWarning("  User {Auth} not admin for {Code}", callerAuth, code);
                return Results.Unauthorized();
            }

            // 3️⃣ Load target via repo
            var target = await userRepo.GetUserById(userId, token);
            if (target == null || target.CompanyId != company.Id)
            {
                log.LogWarning("  User {UserId} not in company {CompanyId}", userId, company.Id);
                return Results.NotFound($"User '{userId}' not found in company '{code}'.");
            }

            // 4️⃣ Map to your slim DTO
            var outDto = new CompanyEmployeeDetailsDto
            {
                Id = target.UserId,
                AuthUserId = target.AuthUserId,
                CompanyCode = code,
                FirstName = target.FirstName,
                LastName = target.LastName,
                Email = target.Email,
                Phone = target.Phone,
                RoleId = target.RoleId,
                Permissions = target.Permissions
            };
            log.LogInformation("← GetCompanyUserById found {UserId}", userId);
            return Results.Ok(outDto);
        }

        public static async Task<IResult> EditCompanyUser(
            string code,
            int userId,
            [FromBody] EditUserDto dto,
            HttpContext ctx,
            [FromServices] IUserRepository userRepo,
            [FromServices] ICompanyRepository companyRepo,
            [FromServices] ILogger<CompanyEndpoints> log)
        {
            log.LogInformation("→ EditCompanyUser {Code}/{UserId}", code, userId);

            // 1️⃣ Authenticate
            int callerAuth;
            try { callerAuth = ResolveAuthUserId(ctx); }
            catch
            {
                log.LogWarning("  Missing/invalid nameid claim");
                return Results.Unauthorized();
            }
            var token = ctx.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var me = await userRepo.GetUserByAuthId(callerAuth, token);
            if (me == null)
            {
                log.LogWarning("  No local user for authId={Auth}", callerAuth);
                return Results.Unauthorized();
            }

            // 2️⃣ Company exists + admin?
            var company = await companyRepo.GetByCodeAsync(code);
            if (company == null)
            {
                log.LogWarning("  Company '{Code}' not found", code);
                return Results.NotFound($"Company '{code}' not found.");
            }
            if (!me.IsCompanyAdmin || !string.Equals(me.CompanyCode, code, StringComparison.OrdinalIgnoreCase))
            {
                log.LogWarning("  User {Auth} not admin for {Code}", callerAuth, code);
                return Results.Unauthorized();
            }

            // 3️⃣ Ensure target belongs to this company
            var target = await userRepo.GetUserById(userId, token);
            if (target == null || target.CompanyId != company.Id)
            {
                log.LogWarning("  User {UserId} not in company {CompanyId}", userId, company.Id);
                return Results.NotFound($"User '{userId}' not found in company '{code}'.");
            }

            // 4️⃣ Update via your repo
            var ok = await userRepo.EditUser(userId, dto);
            if (!ok)
            {
                log.LogWarning("  EditUser failed for {UserId}", userId);
                return Results.BadRequest("Could not update user.");
            }

            log.LogInformation("← EditCompanyUser complete for {UserId}", userId);
            return Results.NoContent();
        }


    }
}
