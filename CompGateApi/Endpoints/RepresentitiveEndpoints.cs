// CompGateApi.Endpoints/RepresentativeEndpoints.cs
using System;
using System.IO;
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
                .Produces<PagedResult<RepresentativeDto>>(200);

            reps.MapGet("/{id:int}", GetRepresentativeById)
                .Produces<RepresentativeDto>(200)
                .Produces(404);

            // POST and PUT both take raw HttpRequest — no antiforgery metadata
            reps.MapPost("/", CreateRepresentative)
                .Produces<RepresentativeDto>(201)
                .Produces(400)
                .Produces(401);

            reps.MapPut("/{id:int}", UpdateRepresentative)
                .Produces<RepresentativeDto>(200)
                .Produces(400)
                .Produces(404)
                .Produces(401);

            reps.MapDelete("/{id:int}", DeleteRepresentative)
                .Produces(200)
                .Produces(404)
                .Produces(401);
        }

        static int GetAuthUserId(HttpContext ctx)
        {
            var raw = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? ctx.User.FindFirst("nameid")?.Value;
            if (!int.TryParse(raw, out var id))
                throw new UnauthorizedAccessException("Missing/invalid 'nameid' claim.");
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

                var all = await repo.GetAllByCompanyAsync(me.CompanyId.Value, searchTerm, searchBy, page, limit);
                var visible = all.Where(r => !r.IsDeleted).ToList();
                var total = await repo.GetCountByCompanyAsync(me.CompanyId.Value, searchTerm, searchBy);

                var dtos = visible.Select(r => new RepresentativeDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Number = r.Number,
                    PassportNumber = r.PassportNumber,
                    IsActive = r.IsActive,
                    IsDeleted = r.IsDeleted,
                    PhotoUrl = r.PhotoUrl,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                }).ToList();

                return Results.Ok(new PagedResult<RepresentativeDto>
                {
                    Data = dtos,
                    Page = page,
                    Limit = limit,
                    TotalRecords = total,
                    TotalPages = (int)Math.Ceiling(total / (double)limit)
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
                if (ent == null || ent.CompanyId != me.CompanyId.Value || ent.IsDeleted)
                    return Results.NotFound("Representative not found.");

                var dto = new RepresentativeDto
                {
                    Id = ent.Id,
                    Name = ent.Name,
                    Number = ent.Number,
                    PassportNumber = ent.PassportNumber,
                    IsActive = ent.IsActive,
                    IsDeleted = ent.IsDeleted,
                    PhotoUrl = ent.PhotoUrl,
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
            HttpContext ctx,
            IUserRepository userRepo,
            IRepresentativeRepository repo,
            ILogger<RepresentativeEndpoints> log)
        {
            var req = ctx.Request;
            try
            {
                var authId = GetAuthUserId(ctx);
                var bearer = req.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null || !me.CompanyId.HasValue)
                    return Results.Unauthorized();

                if (!req.HasFormContentType)
                    return Results.BadRequest("Must be multipart/form-data.");

                var form = await req.ReadFormAsync();
                if (!form.Files.Any())
                    return Results.BadRequest("Photo is required.");

                var photo = form.Files[0];
                var name = form["Name"].ToString();
                var number = form["Number"].ToString();
                var passport = form["PassportNumber"].ToString();

                if (string.IsNullOrWhiteSpace(name)
                 || string.IsNullOrWhiteSpace(number)
                 || string.IsNullOrWhiteSpace(passport))
                {
                    return Results.BadRequest("Name, Number and PassportNumber are required.");
                }

                // save file
                var dir = Path.Combine("wwwroot", "representatives", me.CompanyId.Value.ToString());
                Directory.CreateDirectory(dir);
                var ext = Path.GetExtension(photo.FileName);
                var fn = $"{Guid.NewGuid()}{ext}";
                var fp = Path.Combine(dir, fn);
                await using var fs = File.Create(fp);
                await photo.CopyToAsync(fs);

                var ent = new Representative
                {
                    Name = name,
                    Number = number,
                    PassportNumber = passport,
                    CompanyId = me.CompanyId.Value,
                    IsActive = true,
                    IsDeleted = false,
                    PhotoFileName = fn,
                    PhotoUrl = $"/representatives/{me.CompanyId}/{fn}"
                };

                await repo.CreateAsync(ent);
                log.LogInformation("Created Representative Id={Id}", ent.Id);

                var outDto = new RepresentativeDto
                {
                    Id = ent.Id,
                    Name = ent.Name,
                    Number = ent.Number,
                    PassportNumber = ent.PassportNumber,
                    IsActive = ent.IsActive,
                    IsDeleted = ent.IsDeleted,
                    PhotoUrl = ent.PhotoUrl,
                    CreatedAt = ent.CreatedAt,
                    UpdatedAt = ent.UpdatedAt
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
            HttpContext ctx,
            IUserRepository userRepo,
            IRepresentativeRepository repo,
            ILogger<RepresentativeEndpoints> log)
        {
            var req = ctx.Request;
            try
            {
                var authId = GetAuthUserId(ctx);
                var bearer = req.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null || !me.CompanyId.HasValue)
                    return Results.Unauthorized();

                var ent = await repo.GetByIdAsync(id);
                if (ent == null || ent.CompanyId != me.CompanyId.Value || ent.IsDeleted)
                    return Results.NotFound("Representative not found.");

                if (!req.HasFormContentType)
                    return Results.BadRequest("Must be multipart/form-data.");

                var form = await req.ReadFormAsync();
                ent.Name = form["Name"].ToString() ?? ent.Name;
                ent.Number = form["Number"].ToString() ?? ent.Number;
                ent.PassportNumber = form["PassportNumber"].ToString() ?? ent.PassportNumber;
                if (bool.TryParse(form["IsActive"], out var a)) ent.IsActive = a;

                if (form.Files.Any())
                {
                    // delete old
                    var old = Path.Combine("wwwroot", ent.PhotoUrl.TrimStart('/'));
                    if (File.Exists(old)) File.Delete(old);

                    var photo = form.Files[0];
                    var ext = Path.GetExtension(photo.FileName);
                    var fn = $"{Guid.NewGuid()}{ext}";
                    var dir = Path.Combine("wwwroot", "representatives", me.CompanyId.Value.ToString());
                    Directory.CreateDirectory(dir);
                    var fp = Path.Combine(dir, fn);
                    await using var fs = File.Create(fp);
                    await photo.CopyToAsync(fs);

                    ent.PhotoFileName = fn;
                    ent.PhotoUrl = $"/representatives/{me.CompanyId}/{fn}";
                }

                await repo.UpdateAsync(ent);
                log.LogInformation("Updated Representative Id={Id}", ent.Id);

                var outDto = new RepresentativeDto
                {
                    Id = ent.Id,
                    Name = ent.Name,
                    Number = ent.Number,
                    PassportNumber = ent.PassportNumber,
                    IsActive = ent.IsActive,
                    IsDeleted = ent.IsDeleted,
                    PhotoUrl = ent.PhotoUrl,
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
                if (ent == null || ent.CompanyId != me.CompanyId.Value || ent.IsDeleted)
                    return Results.NotFound("Representative not found.");

                ent.IsDeleted = true;
                await repo.UpdateAsync(ent);

                log.LogInformation("Soft-deleted Representative Id={Id}", ent.Id);
                return Results.Ok();
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Unauthorized in DeleteRepresentative");
                return Results.Unauthorized();
            }
        }
    }
}
