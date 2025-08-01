// ── CompGateApi.Endpoints/RolesPermissionsEndpoints.cs ────────────────
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CompGateApi.Endpoints
{
    public class RolesPermissionsEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var users = app
                .MapGroup("/api/users")
                .RequireAuthorization("RequireCompanyUser");

            // ── ROLE CRUD ─────────────────────────────────────────────────
            users.MapGet("/roles", GetRoles)
                 .WithName("GetRoles")
                 .Produces<List<RoleDto>>(200);

            users.MapGet("/roles/{roleId:int}", GetRoleById)
                 .WithName("GetRoleById")
                 .Produces<RoleDto>(200)
                 .Produces(404);

            users.MapPost("/roles", CreateRole)
                 .WithName("CreateRole")
                 .Accepts<RoleCreateDto>("application/json")
                 .Produces<RoleDto>(201)
                 .Produces(400);

            users.MapPut("/roles/{roleId:int}", UpdateRole)
                 .WithName("UpdateRole")
                 .Accepts<RoleUpdateDto>("application/json")
                 .Produces<RoleDto>(200)
                 .Produces(400)
                 .Produces(404);

            users.MapDelete("/roles/{roleId:int}", DeleteRole)
                 .WithName("DeleteRole")
                 .Produces(204)
                 .Produces(404);

            // ── ASSIGN PERMISSIONS TO ROLE ───────────────────────────────
            users.MapGet("/roles/{roleId:int}/permissions", GetPermissionsByRole)
                .WithName("GetPermissionsByRole")
                .Produces<RolePermissionsOverviewDto>(200);

            users.MapPut("/roles/{roleId:int}/permissions", AssignPermissionsToRole)
                 .WithName("AssignPermissionsToRole")
                 .Accepts<AssignRolePermissionsDto>("application/json")
                 .Produces(200)
                 .Produces(400);

            // ── GLOBAL PERMISSION LOOKUPS ────────────────────────────────
            users.MapGet("/permissions", GetPermissions)
                 .WithName("GetPermissions")
                 .Produces<List<PermissionDto>>(200);

            users.MapGet("/permissions/global", GetPermissionsByGlobal)
                 .WithName("GetPermissionsByGlobal")
                 .Produces<List<PermissionDto>>(200);

            users.MapPost("/permissions", CreatePermission)
                .WithName("CreatePermission")
                .Accepts<PermissionCreateDto>("application/json")
                .Produces<PermissionDto>(201)
                .Produces(400);

            users.MapPut("/permissions/{permissionId:int}", UpdatePermission)
                 .WithName("UpdatePermission")
                 .Accepts<PermissionUpdateDto>("application/json")
                 .Produces<PermissionDto>(200)
                 .Produces(400)
                 .Produces(404);

            users.MapDelete("/permissions/{permissionId:int}", DeletePermission)
                 .WithName("DeletePermission")
                 .Produces(204)
                 .Produces(404);
        }

        // ── Handlers ─────────────────────────────────────────────────────

        static async Task<IResult> GetRoles(
            [FromQuery] bool? isGlobal,
            [FromServices] IRoleRepository repo,
            [FromServices] ILogger<RolesPermissionsEndpoints> log)
        {
            log.LogInformation("Fetching roles (isGlobal={IsGlobal})", isGlobal);
            var list = await repo.GetRolesAsync(isGlobal);
            return Results.Ok(list);
        }

        static async Task<IResult> GetRoleById(
            int roleId,
            [FromServices] IRoleRepository repo,
            [FromServices] ILogger<RolesPermissionsEndpoints> log)
        {
            log.LogInformation("Fetching role {RoleId}", roleId);
            var role = await repo.GetRoleByIdAsync(roleId);
            return role != null
                ? Results.Ok(role)
                : Results.NotFound();
        }

        static async Task<IResult> CreateRole(
            [FromBody] RoleCreateDto dto,
            [FromServices] IRoleRepository repo,
            [FromServices] ILogger<RolesPermissionsEndpoints> log)
        {
            log.LogInformation("Creating role {NameLT}/{NameAR}", dto.NameLT, dto.NameAR);
            var created = await repo.CreateRoleAsync(dto.NameLT, dto.NameAR, dto.Description, dto.IsGlobal);
            return Results.Created($"/api/users/roles/{created.Id}", created);
        }

        static async Task<IResult> UpdateRole(
            int roleId,
            [FromBody] RoleUpdateDto dto,
            [FromServices] IRoleRepository repo,
            [FromServices] ILogger<RolesPermissionsEndpoints> log)
        {
            log.LogInformation("Updating role {RoleId}", roleId);
            var ok = await repo.UpdateRoleAsync(roleId, dto.NameLT, dto.NameAR, dto.Description, dto.IsGlobal);
            if (!ok) return Results.NotFound();
            var updated = await repo.GetRoleByIdAsync(roleId)!;
            return Results.Ok(updated);
        }

        static async Task<IResult> DeleteRole(
            int roleId,
            [FromServices] IRoleRepository repo,
            [FromServices] ILogger<RolesPermissionsEndpoints> log)
        {
            log.LogInformation("Deleting role {RoleId}", roleId);
            var ok = await repo.DeleteRoleAsync(roleId);
            return ok ? Results.NoContent() : Results.NotFound();
        }

        static async Task<IResult> GetPermissions(
            [FromServices] IRoleRepository repo,
            [FromServices] ILogger<RolesPermissionsEndpoints> log)
        {
            log.LogInformation("Fetching all permissions");
            var perms = await repo.GetAllPermissionsAsync();
            return Results.Ok(perms);
        }

        static async Task<IResult> GetPermissionsByRole(
            int roleId,
            [FromQuery] bool? isGlobal,
            [FromServices] IRoleRepository repo,
            [FromServices] ILogger<RolesPermissionsEndpoints> log)
        {
            log.LogInformation("Fetching permissions for role {RoleId} (isGlobal={IsGlobal})",
                               roleId, isGlobal);

            // 1) load all permissions (apply global filter if present)
            var all = await repo.GetAllPermissionsAsync();
            if (isGlobal.HasValue)
                all = all.Where(p => p.IsGlobal == isGlobal.Value).ToList();

            // 2) load assigned ones
            var assigned = await repo.GetPermissionsByRoleAsync(roleId);
            if (isGlobal.HasValue)
                assigned = assigned.Where(p => p.IsGlobal == isGlobal.Value).ToList();

            // 3) compute “available” = in all but not in assigned
            var available = all
              .Where(p => assigned.All(a => a.Id != p.Id))
              .ToList();

            return Results.Ok(new RolePermissionsOverviewDto(
                Assigned: assigned,
                Available: available
            ));
        }

        static async Task<IResult> GetPermissionsByGlobal(
            [FromQuery] bool isGlobal,
            [FromServices] IRoleRepository repo,
            [FromServices] ILogger<RolesPermissionsEndpoints> log)
        {
            log.LogInformation("Fetching permissions where IsGlobal={IsGlobal}", isGlobal);
            var perms = await repo.GetPermissionsByGlobalAsync(isGlobal);
            return Results.Ok(perms);
        }

        static async Task<IResult> AssignPermissionsToRole(
            int roleId,
            [FromBody] AssignRolePermissionsDto dto,
            [FromServices] IRoleRepository repo,
            [FromServices] ILogger<RolesPermissionsEndpoints> log)
        {
            log.LogInformation("Assigning {Count} permissions to role {RoleId}", dto.PermissionIds.Count(), roleId);
            var ok = await repo.AssignPermissionsToRoleAsync(roleId, dto.PermissionIds);
            return ok
                ? Results.Ok("Permissions assigned successfully.")
                : Results.BadRequest("Failed to assign permissions.");
        }


        static async Task<IResult> CreatePermission(
            [FromBody] PermissionCreateDto dto,
            [FromServices] IRoleRepository repo,
            [FromServices] ILogger<RolesPermissionsEndpoints> log)
        {
            log.LogInformation("Creating permission {Name}", dto.NameAr);
            var created = await repo.CreatePermissionAsync(dto.NameAr, dto.NameEn, dto.Description, dto.IsGlobal, dto.Type);
            return Results.Created($"/api/users/permissions/{created.Id}", created);
        }

        static async Task<IResult> UpdatePermission(
            int permissionId,
            [FromBody] PermissionUpdateDto dto,
            [FromServices] IRoleRepository repo,
            [FromServices] ILogger<RolesPermissionsEndpoints> log)
        {
            log.LogInformation("Updating permission {PermissionId}", permissionId);
            var ok = await repo.UpdatePermissionAsync(dto.Id, dto.NameAr, dto.NameEn, dto.Description, dto.IsGlobal, dto.Type);
            if (!ok) return Results.NotFound();
            var updated = new PermissionDto { Id = dto.Id, NameAr = dto.NameAr, NameEn = dto.NameEn, Description = dto.Description, IsGlobal = dto.IsGlobal, Type = dto.Type };
            return Results.Ok(updated);
        }

        static async Task<IResult> DeletePermission(
            int permissionId,
            [FromServices] IRoleRepository repo,
            [FromServices] ILogger<RolesPermissionsEndpoints> log)
        {
            log.LogInformation("Deleting permission {PermissionId}", permissionId);
            var ok = await repo.DeletePermissionAsync(permissionId);
            return ok ? Results.NoContent() : Results.NotFound();
        }


        // ── New DTOs ───────────────────────────────────────────────────────
        public record RoleCreateDto(string NameLT, string NameAR, string Description, bool IsGlobal);
        public record RoleUpdateDto(string NameLT, string NameAR, string Description, bool IsGlobal);
        public record AssignRolePermissionsDto(IEnumerable<int> PermissionIds);

        // at bottom of RolesPermissionsEndpoints.cs
        public record PermissionCreateDto(string NameAr, string NameEn, string Description, bool IsGlobal, string? Type);
        public record PermissionUpdateDto(int Id, string NameAr, string NameEn, string Description, bool IsGlobal, string? Type);

        public record RolePermissionsOverviewDto(
            IEnumerable<PermissionDto> Assigned,
            IEnumerable<PermissionDto> Available
        );

    }
}