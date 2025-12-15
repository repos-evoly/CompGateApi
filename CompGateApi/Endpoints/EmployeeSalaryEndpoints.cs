// CompGateApi.Endpoints/EmployeeSalaryEndpoints.cs
using System.Security.Claims;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Core.Errors;
using CompGateApi.Data.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Endpoints
{
    public class EmployeeSalaryEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var group = app.MapGroup("/api/employees")
                .WithTags("Employees & Salaries")
                .RequireAuthorization("RequireCompanyUser");

            group.MapGet("/", GetAllEmployees);
            group.MapPost("/", CreateEmployee);
            group.MapPut("/{id:int}", UpdateEmployee);
            group.MapPost("/{id:int}/update", UpdateEmployee); // POST alias
            group.MapDelete("/{id:int}", DeleteEmployee);
            group.MapPost("/{id:int}/delete", DeleteEmployee); // POST alias
            group.MapPut("/batch", BatchUpdateEmployees);
            group.MapPost("/batch/update", BatchUpdateEmployees); // POST alias
            group.MapGet("/{id:int}", GetEmployeeById);                         // <-- NEW
            group.MapGet("/salarycycles/{id:int}", GetSalaryCycleById);         // <-- NEW
            group.MapGet("/salarycycles/{cycleId:int}/entries/{entryId:int}",   // <-- NEW
                         GetSalaryEntryById);

            group.MapGet("/salarycycles", GetSalaryCycles);
            group.MapPost("/salarycycles", CreateSalaryCycle);
            group.MapPost("/salarycycles/{id:int}/post", PostSalaryCycle);
            // in RegisterEndpoints
            group.MapPut("/salarycycles/{id:int}", SaveSalaryCycle);
            group.MapPost("/salarycycles/{id:int}/update", SaveSalaryCycle); // POST alias


            var admin = app.MapGroup("/api/admin/salarycycles")
                        .WithTags("Employees & Salaries (Admin)")
                        .RequireAuthorization("RequireAdminUser");

            admin.MapGet("/", AdminGetSalaryCycles)
                 .Produces<PagedResult<SalaryCycleAdminListItemDto>>(200);

            admin.MapGet("/{id:int}", AdminGetSalaryCycleById)
                 .Produces<SalaryCycleAdminDetailDto>(200)
                 .Produces(404);
        }

        private static int GetAuthUserId(HttpContext ctx)
        {
            var raw = ctx.User.FindFirst("nameid")?.Value
                   ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(raw, out var id)) return id;
            throw new UnauthorizedAccessException("Missing/invalid 'nameid' claim.");
        }

        public static async Task<IResult> GetAllEmployees(
            HttpContext ctx,
            [FromServices] IEmployeeSalaryRepository repo,
            [FromServices] IUserRepository userRepo,
            [FromQuery] string? searchTerm,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var user = await userRepo.GetUserByAuthId(authId, bearer);
            if (user?.CompanyId == null) return Results.Unauthorized();

            var employees = await repo.GetAllEmployeesAsync(user.CompanyId.Value, searchTerm, page, limit);
            return Results.Ok(employees);
        }

        public static async Task<IResult> CreateEmployee(
            [FromBody] EmployeeCreateDto dto,
            HttpContext ctx,
            [FromServices] IEmployeeSalaryRepository repo,
            [FromServices] IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var user = await userRepo.GetUserByAuthId(authId, bearer);
            if (user?.CompanyId == null) return Results.Unauthorized();

            var employee = await repo.CreateEmployeeAsync(user.CompanyId.Value, dto);
            return Results.Ok(employee);
        }

        public static async Task<IResult> UpdateEmployee(
            int id,
            [FromBody] EmployeeCreateDto dto,
            HttpContext ctx,
            [FromServices] IEmployeeSalaryRepository repo,
            [FromServices] IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var user = await userRepo.GetUserByAuthId(authId, bearer);
            if (user?.CompanyId == null) return Results.Unauthorized();

            var updated = await repo.UpdateEmployeeAsync(user.CompanyId.Value, id, dto);
            return updated == null ? Results.NotFound() : Results.Ok(updated);
        }

        public static async Task<IResult> DeleteEmployee(
            int id,
            HttpContext ctx,
            [FromServices] IEmployeeSalaryRepository repo,
            [FromServices] IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var user = await userRepo.GetUserByAuthId(authId, bearer);
            if (user?.CompanyId == null) return Results.Unauthorized();

            var result = await repo.DeleteEmployeeAsync(user.CompanyId.Value, id);
            return result ? Results.NoContent() : Results.NotFound();
        }

        public static async Task<IResult> BatchUpdateEmployees(
            [FromBody] List<EmployeeDto> updates,
            HttpContext ctx,
            [FromServices] IEmployeeSalaryRepository repo,
            [FromServices] IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var user = await userRepo.GetUserByAuthId(authId, bearer);
            if (user?.CompanyId == null) return Results.Unauthorized();

            var success = await repo.BatchUpdateAsync(user.CompanyId.Value, updates);
            return success ? Results.Ok("Batch updated successfully.") : Results.BadRequest("Failed to update batch.");
        }


        public static async Task<IResult> GetEmployeeById(
    int id,
    HttpContext ctx,
    IEmployeeSalaryRepository repo,
    IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var user = await userRepo.GetUserByAuthId(authId, bearer);
            if (user?.CompanyId == null) return Results.Unauthorized();

            var dto = await repo.GetEmployeeAsync(user.CompanyId.Value, id);
            return dto == null ? Results.NotFound() : Results.Ok(dto);
        }

        public static async Task<IResult> GetSalaryCycleById(
            int id,
            HttpContext ctx,
            IEmployeeSalaryRepository repo,
            IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var user = await userRepo.GetUserByAuthId(authId, bearer);
            if (user?.CompanyId == null) return Results.Unauthorized();

            var dto = await repo.GetSalaryCycleAsync(user.CompanyId.Value, id);
            return dto == null ? Results.NotFound() : Results.Ok(dto);
        }

        public static async Task<IResult> GetSalaryEntryById(
            int cycleId,
            int entryId,
            HttpContext ctx,
            IEmployeeSalaryRepository repo,
            IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var user = await userRepo.GetUserByAuthId(authId, bearer);
            if (user?.CompanyId == null) return Results.Unauthorized();

            var dto = await repo.GetSalaryEntryAsync(user.CompanyId.Value, cycleId, entryId);
            return dto == null ? Results.NotFound() : Results.Ok(dto);
        }

        public static async Task<IResult> GetSalaryCycles(
            HttpContext ctx,
            [FromServices] IEmployeeSalaryRepository repo,
            [FromServices] IUserRepository userRepo,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var user = await userRepo.GetUserByAuthId(authId, bearer);
            if (user?.CompanyId == null) return Results.Unauthorized();

            var cycles = await repo.GetSalaryCyclesAsync(user.CompanyId.Value, page, limit);
            return Results.Ok(cycles);
        }

        public static async Task<IResult> CreateSalaryCycle(
            [FromBody] SalaryCycleCreateDto dto,
            HttpContext ctx,
            [FromServices] IEmployeeSalaryRepository repo,
            [FromServices] IUserRepository userRepo,
            [FromServices] ILogger<EmployeeSalaryEndpoints> log)
        {
            try
            {
                var authId = GetAuthUserId(ctx);
                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var user = await userRepo.GetUserByAuthId(authId, bearer);

                if (user?.CompanyId == null)
                    return Results.Unauthorized();

                var cycle = await repo.CreateSalaryCycleAsync(
                                user.CompanyId.Value,
                                user.UserId,
                                dto);

                return Results.Ok(cycle);
            }
            /* -- domain / validation errors we want to surface to caller -- */
            catch (InvalidOperationException ex)
            {
                log.LogWarning(ex, "CreateSalaryCycle business-rule failure");
                return Results.BadRequest(new { error = ex.Message });
            }
            /* -- anything else is still a 500, but now **logged** -- */
            catch (Exception ex)
            {
                log.LogError(ex, "Unhandled error in CreateSalaryCycle");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        public static async Task<IResult> PostSalaryCycle(
    int id,
    HttpContext ctx,
    [FromServices] IEmployeeSalaryRepository repo,
    [FromServices] IUserRepository userRepo,
    ILogger<EmployeeSalaryEndpoints> log)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var user = await userRepo.GetUserByAuthId(authId, bearer);
            if (user?.CompanyId == null) return Results.Unauthorized();

            try
            {
                var result = await repo.PostSalaryCycleAsync(user.CompanyId.Value, id, user.UserId);
                if (result is null) return Results.NotFound();

                // shape: include your cycle and the bank’s per-entry outcome
                return Results.Ok(new
                {
                    cycle = result,
                });
            }
            catch (PayrollException ex)
            {
                log.LogWarning(ex, "PostSalaryCycle business-rule failure");
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Unhandled error in PostSalaryCycle");
                return Results.StatusCode(500);
            }
        }


        public static async Task<IResult> SaveSalaryCycle(
        int id, SalaryCycleSaveDto dto, HttpContext ctx,
        IEmployeeSalaryRepository repo, IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var token = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, token);
            if (me?.CompanyId == null) return Results.Unauthorized();

            var saved = await repo.SaveSalaryCycleAsync(me.CompanyId.Value, id, dto);
            return saved == null ? Results.NotFound() : Results.Ok(saved);
        }


        // ── ADMIN HANDLERS ────────────────────────────────────────────────────────────────
        public static async Task<IResult> AdminGetSalaryCycles(
            [FromServices] IEmployeeSalaryRepository repo,
            [FromQuery] string? companyCode,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            var total = await repo.AdminGetSalaryCyclesCountAsync(companyCode, searchTerm, searchBy, from, to);
            var list = await repo.AdminGetSalaryCyclesAsync(companyCode, searchTerm, searchBy, from, to, page, limit);

            return Results.Ok(new PagedResult<SalaryCycleAdminListItemDto>
            {
                Data = list.Data,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit)
            });
        }

        public static async Task<IResult> AdminGetSalaryCycleById(
            int id,
            [FromServices] IEmployeeSalaryRepository repo)
        {
            var dto = await repo.AdminGetSalaryCycleAsync(id);
            return dto == null ? Results.NotFound() : Results.Ok(dto);
        }

    }
}
