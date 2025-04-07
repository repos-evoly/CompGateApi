using Microsoft.AspNetCore.Mvc;
using BlockingApi.Data.Context;
using AutoMapper;
using BlockingApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using BlockingApi.Core.Dtos;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlockingApi.Abstractions;

namespace BlockingApi.Endpoints
{
    public class AreasEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var areas = app.MapGroup("/api/areas").RequireAuthorization("requireAuthUser");

            // GET endpoint with optional search and pagination.
            areas.MapGet("/", GetAll)
                .WithName("GetAreas")
                .Produces<IEnumerable<AreaDto>>(200);

            areas.MapGet("/{id:int}", GetById)
                .WithName("GetAreaById")
                .Produces<AreaDto>(200)
                .Produces(404);

            areas.MapPost("/", Create)
                .WithName("CreateArea")
                .Accepts<EditAreaDto>("application/json")
                .Produces<AreaDto>(201)
                .Produces(400);

            areas.MapPut("/{id:int}", Update)
                .WithName("UpdateArea")
                .Accepts<EditAreaDto>("application/json")
                .Produces<AreaDto>(200)
                .Produces(400);

            areas.MapDelete("/{id:int}", Delete)
                .WithName("DeleteArea")
                .Produces(204)
                .Produces(400);
        }

        // GET all areas with optional filtering (by search/searchBy) and pagination.
        public static async Task<IResult> GetAll(
            [FromServices] BlockingApiDbContext context,
            [FromServices] IMapper mapper,
            [FromQuery] string? search,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 100000)
        {
            // Include both Branches and HeadOfSection.
            var query = context.Areas
                .Include(a => a.Branches)
                .Include(a => a.HeadOfSection)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search) && !string.IsNullOrWhiteSpace(searchBy))
            {
                switch (searchBy.ToLower())
                {
                    case "name":
                        query = query.Where(a => a.Name.Contains(search));
                        break;
                    case "headofsectionid":
                        if (int.TryParse(search, out int headId))
                        {
                            query = query.Where(a => a.HeadOfSectionId == headId);
                        }
                        break;
                    case "branchname":
                        query = query.Where(a => a.Branches.Any(b => b.Name.Contains(search)));
                        break;
                    default:
                        // Optionally ignore unrecognized searchBy values.
                        break;
                }
            }

            // Apply ordering and pagination.
            query = query.OrderBy(a => a.Id)
                         .Skip((page - 1) * limit)
                         .Take(limit);

            var areas = await query.ToListAsync();
            var areaDtos = mapper.Map<IEnumerable<AreaDto>>(areas).ToList();

            // Manually assign HeadOfSectionName for each DTO.
            foreach (var dto in areaDtos)
            {
                var areaEntity = areas.FirstOrDefault(a => a.Id == dto.Id);
                if (areaEntity?.HeadOfSection != null)
                {
                    dto.HeadOfSectionName = $"{areaEntity.HeadOfSection.FirstName} {areaEntity.HeadOfSection.LastName}".Trim();
                }
                else
                {
                    dto.HeadOfSectionName = null;
                }
            }

            return TypedResults.Ok(areaDtos);
        }

        // GET by ID: Return a single area with its branches and head of section name.
        public static async Task<IResult> GetById(
            [FromServices] BlockingApiDbContext context,
            [FromServices] IMapper mapper,
            int id)
        {
            var area = await context.Areas
                .Include(a => a.Branches)
                .Include(a => a.HeadOfSection)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (area == null)
                return TypedResults.NotFound("Area not found.");

            var dto = mapper.Map<AreaDto>(area);
            if (area.HeadOfSection != null)
            {
                dto.HeadOfSectionName = $"{area.HeadOfSection.FirstName} {area.HeadOfSection.LastName}".Trim();
            }
            else
            {
                dto.HeadOfSectionName = null;
            }
            return TypedResults.Ok(dto);
        }

        public static async Task<IResult> Create(
            [FromServices] BlockingApiDbContext context,
            [FromServices] IMapper mapper,
            [FromBody] EditAreaDto areaDto)
        {
            if (areaDto == null) return TypedResults.BadRequest("Invalid area data.");

            var area = mapper.Map<Area>(areaDto);
            context.Areas.Add(area);
            await context.SaveChangesAsync();

            var dto = mapper.Map<AreaDto>(area);
            if (area.HeadOfSection != null)
            {
                dto.HeadOfSectionName = $"{area.HeadOfSection.FirstName} {area.HeadOfSection.LastName}".Trim();
            }
            return TypedResults.Created($"/api/areas/{area.Id}", dto);
        }

        public static async Task<IResult> Update(
            [FromServices] BlockingApiDbContext context,
            [FromServices] IMapper mapper,
            int id,
            [FromBody] EditAreaDto areaDto)
        {
            var area = await context.Areas.FindAsync(id);
            if (area == null) return TypedResults.BadRequest("Invalid area data.");

            mapper.Map(areaDto, area);
            context.Areas.Update(area);
            await context.SaveChangesAsync();

            var dto = mapper.Map<AreaDto>(area);
            if (area.HeadOfSection != null)
            {
                dto.HeadOfSectionName = $"{area.HeadOfSection.FirstName} {area.HeadOfSection.LastName}".Trim();
            }
            return TypedResults.Ok(dto);
        }

        public static async Task<IResult> Delete(
            [FromServices] BlockingApiDbContext context,
            int id)
        {
            var area = await context.Areas.FindAsync(id);
            if (area == null) return TypedResults.NotFound("Area not found.");

            context.Areas.Remove(area);
            await context.SaveChangesAsync();

            return TypedResults.NoContent();
        }
    }
}
