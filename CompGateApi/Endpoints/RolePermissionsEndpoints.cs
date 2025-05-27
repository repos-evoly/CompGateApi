using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using Microsoft.AspNetCore.Mvc;

public class RolesPermissionsEndpoints : IEndpoints
{
    public void RegisterEndpoints(WebApplication app)
    {
        var users = app
            .MapGroup("/api/users")
            .RequireAuthorization("RequireCompanyUser");

        users.MapGet("/roles", GetRoles)
             .WithName("GetRoles")
             .Produces<List<RoleDto>>(200);

        users.MapGet("/roles/{roleId:int}/permissions", GetPermissionsByRole)
             .WithName("GetPermissionsByRole")
             .Produces<List<PermissionDto>>(200);

        users.MapGet("/permissions", GetPermissions)
             .WithName("GetPermissions")
             .Produces<List<PermissionDto>>(200);

        users.MapGet("/permissions/global", GetPermissionsByGlobal)
             .WithName("GetPermissionsByGlobal")
             .Produces<List<PermissionDto>>(200);
    }

    static async Task<IResult> GetRoles(
        [FromQuery] bool? isGlobal,
        [FromServices] IUserRepository repo,
        [FromServices] ILogger<RolesPermissionsEndpoints> log)
    {
        log.LogInformation("Fetching roles (isGlobal={IsGlobal})", isGlobal);
        var list = await repo.GetRolesAsync(isGlobal);
        return Results.Ok(list);
    }

    static async Task<IResult> GetPermissions(
        [FromServices] IUserRepository repo,
        [FromServices] ILogger<RolesPermissionsEndpoints> log)
    {
        log.LogInformation("Fetching all permissions");
        var perms = await repo.GetPermissions();
        var dto = perms.Select(p => new PermissionDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            IsGlobal = p.IsGlobal
        }).ToList();
        return Results.Ok(dto);
    }

    static async Task<IResult> GetPermissionsByRole(
        int roleId,
        [FromServices] IUserRepository repo,
        [FromServices] ILogger<RolesPermissionsEndpoints> log)
    {
        log.LogInformation("Fetching permissions for RoleId={RoleId}", roleId);
        var perms = await repo.GetPermissionsByRoleAsync(roleId);
        var dto = perms.Select(p => new PermissionDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            IsGlobal = p.IsGlobal
        }).ToList();
        return Results.Ok(dto);
    }

    static async Task<IResult> GetPermissionsByGlobal(
        [FromQuery] bool isGlobal,
        [FromServices] IUserRepository repo,
        [FromServices] ILogger<RolesPermissionsEndpoints> log)
    {
        log.LogInformation("Fetching permissions for IsGlobal={IsGlobal}", isGlobal);
        var perms = await repo.GetPermissionsByGlobalAsync(isGlobal);
        var dto = perms.Select(p => new PermissionDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            IsGlobal = p.IsGlobal
        }).ToList();
        return Results.Ok(dto);
    }
}
