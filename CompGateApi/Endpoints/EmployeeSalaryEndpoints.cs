// CompGateApi.Endpoints/EmployeeSalaryEndpoints.cs
using System.Security.Claims;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
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
            group.MapDelete("/{id:int}", DeleteEmployee);
            group.MapPut("/batch", BatchUpdateEmployees);

            group.MapGet("/salarycycles", GetSalaryCycles);
            group.MapPost("/salarycycles", CreateSalaryCycle);
            group.MapPost("/salarycycles/{id:int}/post", PostSalaryCycle);
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
            [FromServices] IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var user = await userRepo.GetUserByAuthId(authId, bearer);
            if (user?.CompanyId == null) return Results.Unauthorized();

            var created = await repo.CreateSalaryCycleAsync(user.CompanyId.Value, user.UserId, dto);
            return Results.Ok(created);
        }

        public static async Task<IResult> PostSalaryCycle(
            int id,
            HttpContext ctx,
            [FromServices] IEmployeeSalaryRepository repo,
            [FromServices] IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var user = await userRepo.GetUserByAuthId(authId, bearer);
            if (user?.CompanyId == null) return Results.Unauthorized();

            var result = await repo.PostSalaryCycleAsync(user.CompanyId.Value, id, user.UserId);
            return result == null ? Results.NotFound() : Results.Ok(result);
        }
    }
}
