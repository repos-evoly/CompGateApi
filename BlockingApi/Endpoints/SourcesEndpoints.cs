using BlockingApi.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BlockingApi.Core.Abstractions;
using AutoMapper;
using BlockingApi.Data.Models;
using BlockingApi.Abstractions;

namespace BlockingApi.Endpoints
{
    public class SourcesEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var sources = app.MapGroup("/api/sources").RequireAuthorization("requireAuthUser");

            sources.MapGet("/", GetAll)
                .WithName("GetSources")
                .Produces<IEnumerable<SourceDto>>(200);

            sources.MapGet("/{id:int}", GetById)
                .WithName("GetSourceById")
                .Produces<SourceDto>(200)
                .Produces(404);

            sources.MapPost("/", Create)
                .WithName("CreateSource")
                .Accepts<EditSourceDto>("application/json")
                .Produces<SourceDto>(201)
                .Produces(400);

            sources.MapPut("/{id:int}", Update)
                .WithName("UpdateSource")
                .Accepts<EditSourceDto>("application/json")
                .Produces<SourceDto>(200)
                .Produces(400);

            sources.MapDelete("/{id:int}", Delete)
                .WithName("DeleteSource")
                .Produces(204)
                .Produces(400);
        }

        public static async Task<IResult> GetAll([FromServices] IUnitOfWork unitOfWork, [FromServices] IMapper mapper)
        {
            var sources = await unitOfWork.Sources.GetAll();
            return TypedResults.Ok(mapper.Map<IEnumerable<SourceDto>>(sources));
        }

        public static async Task<IResult> GetById([FromServices] IUnitOfWork unitOfWork, [FromServices] IMapper mapper, int id)
        {
            var source = await unitOfWork.Sources.GetById(s => s.Id == id);
            return source != null ? TypedResults.Ok(mapper.Map<SourceDto>(source)) : TypedResults.NotFound("Source not found.");
        }

        [Authorize(Roles = "Admin")]
        public static async Task<IResult> Create([FromServices] IUnitOfWork unitOfWork, [FromServices] IMapper mapper, [FromBody] EditSourceDto sourceDto)
        {
            if (sourceDto == null) return TypedResults.BadRequest("Invalid source data.");

            var source = mapper.Map<Source>(sourceDto);
            await unitOfWork.Sources.Create(source);
            await unitOfWork.SaveAsync();

            return TypedResults.Created($"/api/sources/{source.Id}", mapper.Map<SourceDto>(source));
        }

        [Authorize(Roles = "Admin")]
        public static async Task<IResult> Update([FromServices] IUnitOfWork unitOfWork, [FromServices] IMapper mapper, int id, [FromBody] EditSourceDto sourceDto)
        {
            var source = await unitOfWork.Sources.GetById(s => s.Id == id);
            if (source == null) return TypedResults.BadRequest("Invalid source data.");

            mapper.Map(sourceDto, source);
            unitOfWork.Sources.Update(source);
            await unitOfWork.SaveAsync();

            return TypedResults.Ok(mapper.Map<SourceDto>(source));
        }

        [Authorize(Roles = "Admin")]
        public static async Task<IResult> Delete([FromServices] IUnitOfWork unitOfWork, int id)
        {
            var source = await unitOfWork.Sources.GetById(s => s.Id == id);
            if (source == null) return TypedResults.NotFound("Source not found.");

            unitOfWork.Sources.Delete(source);
            await unitOfWork.SaveAsync();

            return TypedResults.NoContent();
        }
    }
}
