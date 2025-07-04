// CompGateApi.Endpoints/RepresentativeEndpoints.cs
using System;
using System.Linq;
using System.Security.Claims;
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
    public class RepresentativeEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var reps = app
                .MapGroup("/api/representatives")
                .WithTags("Representatives")
                .RequireAuthorization("RequireCompanyUser");

            reps.MapGet("/", GetRepresentatives)
                .WithName("GetRepresentatives")
                .Produces<PagedResult<RepresentativeDto>>(200);

            reps.MapGet("/{id:int}", GetRepresentativeById)
                .WithName("GetRepresentativeById")
                .Produces<RepresentativeDto>(200)
                .Produces(404);

            reps.MapPost("/", CreateRepresentative)
                .WithName("CreateRepresentative")
                .Accepts<RepresentativeCreateDto>("application/json")
                .Produces<RepresentativeDto>(201)
                .Produces(400)
                .Produces(401);

            reps.MapPut("/{id:int}", UpdateRepresentative)
                .WithName("UpdateRepresentative")
                .Accepts<RepresentativeUpdateDto>("application/json")
                .Produces<RepresentativeDto>(200)
                .Produces(400)
                .Produces(404)
                .Produces(401);

            reps.MapDelete("/{id:int}", DeleteRepresentative)
                .WithName("DeleteRepresentative")
                .Produces(200)
                .Produces(404)
                .Produces(401);
        }

        private static int GetAuthUserId(HttpContext ctx)
        {
            var raw = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? ctx.User.FindFirst("nameid")?.Value;
            if (!int.TryParse(raw, out var id))
                throw new UnauthorizedAccessException(
                    $"Missing or invalid 'nameid' claim. Raw='{raw}'");
            return id;
        }

        public static async Task<IResult> GetRepresentatives(
            HttpContext ctx,
            IUserRepository userRepo,
            IRepresentativeRepository repo,
            ILogger<RepresentativeEndpoints> log,
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

                var cid = me.CompanyId.Value;
                var list = await repo.GetAllByCompanyAsync(cid, searchTerm, searchBy, page, limit);
                var total = await repo.GetCountByCompanyAsync(cid, searchTerm, searchBy);

                var dtos = list.Select(r => new RepresentativeDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Number = r.Number,
                    IsActive = r.IsActive,
                    PassportNumber = r.PassportNumber,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                }).ToList();

                return Results.Ok(new PagedResult<RepresentativeDto>
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
                log.LogError(ex, "Unauthorized in GetRepresentatives");
                return Results.Unauthorized();
            }
        }

        public static async Task<IResult> GetRepresentativeById(
            int id,
            HttpContext ctx,
            IUserRepository userRepo,
            IRepresentativeRepository repo,
            ILogger<RepresentativeEndpoints> log)
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
                    return Results.NotFound("Representative not found.");

                var dto = new RepresentativeDto
                {
                    Id = ent.Id,
                    Name = ent.Name,
                    Number = ent.Number,
                    PassportNumber = ent.PassportNumber,
                    IsActive = ent.IsActive,
                    CreatedAt = ent.CreatedAt,
                    UpdatedAt = ent.UpdatedAt
                };
                return Results.Ok(dto);
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Unauthorized in GetRepresentativeById");
                return Results.Unauthorized();
            }
        }

        public static async Task<IResult> CreateRepresentative(
            [FromBody] RepresentativeCreateDto dto,
            HttpContext ctx,
            IUserRepository userRepo,
            IRepresentativeRepository repo,
            ILogger<RepresentativeEndpoints> log)
        {


            try
            {
                var authId = GetAuthUserId(ctx);
                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null || !me.CompanyId.HasValue)
                    return Results.Unauthorized();

                var ent = new Representative
                {
                    Name = dto.Name,
                    Number = dto.Number,
                    PassportNumber = dto.PassportNumber,
                    CompanyId = me.CompanyId.Value,
                    IsActive = true,
                };

                await repo.CreateAsync(ent);
                log.LogInformation("Created Representative Id={Id}", ent.Id);

                var outDto = new RepresentativeDto
                {
                    Id = ent.Id,
                    Name = ent.Name,
                    Number = ent.Number,
                    PassportNumber = ent.PassportNumber,
                    CreatedAt = ent.CreatedAt,
                    UpdatedAt = ent.UpdatedAt,
                    IsActive = ent.IsActive
                };

                return Results.Created($"/api/representatives/{ent.Id}", outDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Unauthorized in CreateRepresentative");
                return Results.Unauthorized();
            }
        }

        public static async Task<IResult> UpdateRepresentative(
            int id,
            [FromBody] RepresentativeUpdateDto dto,
            HttpContext ctx,
            IUserRepository userRepo,
            IRepresentativeRepository repo,
            ILogger<RepresentativeEndpoints> log)
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
                    return Results.NotFound("Representative not found.");

                ent.Name = dto.Name;
                ent.Number = dto.Number;
                ent.PassportNumber = dto.PassportNumber;

                await repo.UpdateAsync(ent);
                log.LogInformation("Updated Representative Id={Id}", ent.Id);

                var outDto = new RepresentativeDto
                {
                    Id = ent.Id,
                    Name = ent.Name,
                    Number = ent.Number,
                    PassportNumber = ent.PassportNumber,
                    CreatedAt = ent.CreatedAt,
                    UpdatedAt = ent.UpdatedAt
                };
                return Results.Ok(outDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Unauthorized in UpdateRepresentative");
                return Results.Unauthorized();
            }
        }

        public static async Task<IResult> DeleteRepresentative(
            int id,
            HttpContext ctx,
            IUserRepository userRepo,
            IRepresentativeRepository repo,
            ILogger<RepresentativeEndpoints> log)
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
                    return Results.NotFound("Representative not found.");

                await repo.DeleteAsync(id);
                log.LogInformation("Deleted Representative Id={Id}", id);
                return Results.Ok("Representative deleted successfully.");
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Unauthorized in DeleteRepresentative");
                return Results.Unauthorized();
            }
        }
    }
}
