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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace CompGateApi.Endpoints
{
    public class CompanyEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var companies = app.MapGroup("/api/companies")
                               .WithTags("Companies");

            // 1️⃣ Public KYC lookup
            companies.MapGet("/kyc/{code}", LookupKyc)
                     .WithName("LookupKyc")
                     .Produces(200)
                     .Produces(400);

            // 2️⃣ Register under-review admin
            companies.MapPost("/register", RegisterCompany)
                     .WithName("RegisterCompany")
                     .Accepts<CompanyRegistrationDto>("application/json")
                     .Produces(201)
                     .Produces(400);

            // ── Admin portal ────────────────────────────────────────────────────
            var admin = companies.MapGroup("/admin")
                                 .WithTags("Companies")
                                 .RequireAuthorization("RequireAdminUser");

            // 3a) List / filter companies
            admin.MapGet("/", GetAllCompanies)
                 .WithName("GetAllCompanies")
                 .Produces<PagedResult<CompanyListDto>>(200);

            // 3b) Approve/reject company KYC
            admin.MapPut("/{code}/status", UpdateCompanyStatus)
                 .WithName("UpdateCompanyStatus")
                 .Accepts<CompanyStatusUpdateDto>("application/json")
                 .Produces(200)
                 .Produces(404);

            // ── Company-admin: manage employees ────────────────────────────────
            var employees = companies
                .MapGroup("/{code}/users")
                .WithTags("CompanyUsers")
                .RequireAuthorization("RequireCompanyUser");

            employees.MapPost("/", AddCompanyUser)
                     .WithName("AddCompanyUser")
                     .Accepts<CompanyEmployeeRegistrationDto>("application/json")
                     .Produces<CompanyEmployeeDetailsDto>(201)
                     .Produces(400)
                     .Produces(401);

            employees.MapGet("/", GetCompanyUsers)
                     .WithName("GetCompanyUsers")
                     .Produces<List<CompanyEmployeeDetailsDto>>(200);
        }

        // ────────────────────────────────────────────────────────────────────────
        public static async Task<IResult> LookupKyc(
            string code,
            [FromServices] ICompanyRepository repo)
        {
            if (code.Length != 6)
                return Results.BadRequest("Must be exactly 6 digits.");

            var kyc = await repo.LookupKycAsync(code);
            if (kyc == null || string.IsNullOrWhiteSpace(kyc.companyId))
                return Results.Ok(new { hasKyc = false });

            return Results.Ok(new { hasKyc = true, data = kyc });
        }

        public static async Task<IResult> RegisterCompany(
            [FromBody] CompanyRegistrationDto dto,
            [FromServices] ICompanyRepository repo)
        {
            if (!await repo.CanRegisterCompanyAsync(dto.CompanyId))
                return Results.BadRequest("Please complete KYC lookup first.");

            var ok = await repo.RegisterCompanyAdminAsync(dto);
            return ok
                ? Results.Created($"/api/companies/{dto.CompanyId}", null)
                : Results.BadRequest("Registration failed.");
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

            var list = await repo.GetAllCompaniesAsync(searchTerm, status, page, limit);
            var total = await repo.GetCompaniesCountAsync(searchTerm, status);

            return Results.Ok(new PagedResult<CompanyListDto>
            {
                Data = list,
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

        // ────────────────────────────────────────────────────────────────────────
        static int ResolveAuthUserId(HttpContext ctx) =>
            int.TryParse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
              ? id
              : throw new UnauthorizedAccessException();

        public static async Task<IResult> AddCompanyUser(
            string code,
            [FromBody] CompanyEmployeeRegistrationDto dto,
            HttpContext ctx,
            [FromServices] IUserRepository userRepo,
            [FromServices] IValidator<CompanyEmployeeRegistrationDto> validator,
            [FromServices] IHttpClientFactory httpFactory,
            [FromServices] ILogger<CompanyEndpoints> log)
        {
            // 1️⃣ Validate input
            var vr = await validator.ValidateAsync(dto);
            if (!vr.IsValid)
                return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));

            // 2️⃣ Caller must be the company-admin
            var callerAuth = ResolveAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var me = await userRepo.GetUserByAuthId(callerAuth, bearer);
            if (me == null || me.CompanyId != code)
                return Results.Unauthorized();

            // 3️⃣ Register in Auth service
            var client = httpFactory.CreateClient();
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
            var resp = await client.PostAsJsonAsync(
                "http://10.3.3.11/compauthapi/api/auth/register", authPayload);
            if (!resp.IsSuccessStatusCode)
            {
                log.LogError("Auth register failed: {Status}", resp.StatusCode);
                return Results.BadRequest("External auth registration failed.");
            }
            var body = await resp.Content.ReadAsStringAsync();
            var reg = JsonSerializer.Deserialize<AuthRegisterResponseDto>(body,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            if (reg.userId == 0)
                return Results.BadRequest("Auth returned invalid userId.");

            // 4️⃣ Persist locally
            var user = new User
            {
                AuthUserId = reg.userId,
                CompanyId = code,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Phone = dto.Phone,
                RoleId = dto.RoleId,
                IsCompanyAdmin = false,
                ServicePackageId = me.ServicePackageId
            };
            await userRepo.AddUser(user);

            // 5️⃣ Build return DTO
            var perms = (await userRepo.GetUserPermissions(user.Id))
                            .Where(p => p.HasPermission == 1)
                            .Select(p => p.PermissionName)
                            .ToList();

            var outDto = new CompanyEmployeeDetailsDto
            {
                Id = user.Id,
                AuthUserId = user.AuthUserId,
                CompanyId = user.CompanyId!,
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
            [FromServices] IUserRepository userRepo)
        {
            var callerAuth = ResolveAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var me = await userRepo.GetUserByAuthId(callerAuth, bearer);
            if (me == null || me.CompanyId != code)
                return Results.Unauthorized();

            var all = await userRepo.GetUsersAsync(null, null, 1, int.MaxValue, bearer);
            var ours = all
                .Where(u => u.CompanyId == code && u.AuthUserId != me.AuthUserId)
                .Select(u => new CompanyEmployeeDetailsDto
                {
                    Id = u.UserId,
                    AuthUserId = u.AuthUserId,
                    CompanyId = u.CompanyId!,
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
    }
}
