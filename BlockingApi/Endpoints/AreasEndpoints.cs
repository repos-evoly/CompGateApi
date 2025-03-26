using BlockingApi.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BlockingApi.Data.Context;
using AutoMapper;
using BlockingApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using BlockingApi.Abstractions;

namespace BlockingApi.Endpoints
{
    public class AreasEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var areas = app.MapGroup("/api/areas").RequireAuthorization("requireAuthUser");

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

        public static async Task<IResult> GetAll([FromServices] BlockingApiDbContext context, [FromServices] IMapper mapper)
        {
            var areas = await context.Areas.Include(a => a.Branches).ToListAsync();
            return TypedResults.Ok(mapper.Map<IEnumerable<AreaDto>>(areas));
        }

        public static async Task<IResult> GetById([FromServices] BlockingApiDbContext context, [FromServices] IMapper mapper, int id)
        {
            var area = await context.Areas.Include(a => a.Branches).FirstOrDefaultAsync(a => a.Id == id);
            return area != null ? TypedResults.Ok(mapper.Map<AreaDto>(area)) : TypedResults.NotFound("Area not found.");
        }

        [Authorize(Roles = "Admin")]
        public static async Task<IResult> Create([FromServices] BlockingApiDbContext context, [FromServices] IMapper mapper, [FromBody] EditAreaDto areaDto)
        {
            if (areaDto == null) return TypedResults.BadRequest("Invalid area data.");

            var area = mapper.Map<Area>(areaDto);
            context.Areas.Add(area);
            await context.SaveChangesAsync();

            return TypedResults.Created($"/api/areas/{area.Id}", mapper.Map<AreaDto>(area));
        }

        [Authorize(Roles = "Admin")]
        public static async Task<IResult> Update([FromServices] BlockingApiDbContext context, [FromServices] IMapper mapper, int id, [FromBody] EditAreaDto areaDto)
        {
            var area = await context.Areas.FindAsync(id);
            if (area == null) return TypedResults.BadRequest("Invalid area data.");

            mapper.Map(areaDto, area);
            context.Areas.Update(area);
            await context.SaveChangesAsync();

            return TypedResults.Ok(mapper.Map<AreaDto>(area));
        }

        [Authorize(Roles = "Admin")]
        public static async Task<IResult> Delete([FromServices] BlockingApiDbContext context, int id)
        {
            var area = await context.Areas.FindAsync(id);
            if (area == null) return TypedResults.NotFound("Area not found.");

            context.Areas.Remove(area);
            await context.SaveChangesAsync();

            return TypedResults.NoContent();
        }
    }
}
