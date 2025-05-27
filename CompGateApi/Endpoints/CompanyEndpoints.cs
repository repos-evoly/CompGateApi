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
        }

        static int ResolveAuthUserId(HttpContext ctx)
        {
            var raw = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? ctx.User.FindFirst("nameid")?.Value;
            if (!int.TryParse(raw, out var id))
                throw new UnauthorizedAccessException("Missing or invalid 'nameid' claim.");
            return id;
        }

        public static async Task<IResult> LookupKyc(
            string code,
            [FromServices] ICompanyRepository repo)
        {
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
            [FromQuery] KycStatus? status = null,
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
                KycStatus = c.KycStatus,
                KycStatusMessage = c.KycStatusMessage,
                KycRequestedAt = c.KycRequestedAt,
                KycReviewedAt = c.KycReviewedAt,
                KycBranchId = c.KycBranchId,
                KycLegalCompanyName = c.KycLegalCompanyName,
                KycLegalCompanyNameLt = c.KycLegalCompanyNameLt,
                KycMobile = c.KycMobile,
                KycNationality = c.KycNationality,
                KycCity = c.KycCity
            };

            return Results.Ok(dto);
        }

    }
}
