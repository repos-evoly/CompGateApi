using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using AutoMapper;
using CompGateApi.Abstractions;

namespace CompGateApi.Endpoints
{
    public class EconomicSectorEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var group = app.MapGroup("/api/economic-sectors").RequireAuthorization("requireCompanyUser");

            group.MapGet("/", GetAll);
            group.MapGet("/{id:int}", GetById);
            group.MapPost("/", Create);
            group.MapPut("/{id:int}", Update);
            group.MapPost("/{id:int}/update", Update); // POST alias
            group.MapDelete("/{id:int}", Delete);
            group.MapPost("/{id:int}/delete", Delete); // POST alias
        }

        public static async Task<IResult> GetAll(
            [FromServices] IEconomicSectorRepository repo,
            [FromServices] IMapper mapper,
            [FromQuery] string? searchTerm,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 100000)
        {
            var list = await repo.GetAllAsync(searchTerm, page, limit);
            var total = await repo.GetCountAsync(searchTerm);
            var dto = mapper.Map<List<EconomicSectorDto>>(list);
            int pages = (int)Math.Ceiling((double)total / limit);
            return Results.Ok(new { Data = dto, TotalPages = pages });
        }

        public static async Task<IResult> GetById(
            int id,
            [FromServices] IEconomicSectorRepository repo,
            [FromServices] IMapper mapper)
        {
            var entity = await repo.GetByIdAsync(id);
            if (entity == null) return Results.NotFound("Not found.");
            return Results.Ok(mapper.Map<EconomicSectorDto>(entity));
        }

        public static async Task<IResult> Create(
            [FromBody] EconomicSectorCreateDto dto,
            [FromServices] IEconomicSectorRepository repo,
            [FromServices] IMapper mapper)
        {
            

            var entity = mapper.Map<EconomicSector>(dto);
            await repo.CreateAsync(entity);
            return Results.Created($"/api/economic-sectors/{entity.Id}", mapper.Map<EconomicSectorDto>(entity));
        }

        public static async Task<IResult> Update(
            int id,
            [FromBody] EconomicSectorUpdateDto dto,
            [FromServices] IEconomicSectorRepository repo,
            [FromServices] IMapper mapper)
          
        {
            var entity = await repo.GetByIdAsync(id);
            if (entity == null) return Results.NotFound("Not found.");

          

            mapper.Map(dto, entity);
            await repo.UpdateAsync(entity);
            return Results.Ok(mapper.Map<EconomicSectorDto>(entity));
        }

        public static async Task<IResult> Delete(
            int id,
            [FromServices] IEconomicSectorRepository repo)
        {
            var entity = await repo.GetByIdAsync(id);
            if (entity == null) return Results.NotFound("Not found.");
            await repo.DeleteAsync(id);
            return Results.Ok("Deleted successfully.");
        }
    }
}
