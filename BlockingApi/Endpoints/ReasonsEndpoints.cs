using BlockingApi.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BlockingApi.Core.Abstractions;
using AutoMapper;
using BlockingApi.Data.Models;
using BlockingApi.Abstractions;

namespace BlockingApi.Endpoints
{
    public class ReasonsEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var reasons = app.MapGroup("/api/reasons").RequireAuthorization("requireAuthUser");

            reasons.MapGet("/", GetAll)
                .WithName("GetReasons")
                .Produces<IEnumerable<ReasonDto>>(200);

            reasons.MapGet("/{id:int}", GetById)
                .WithName("GetReasonById")
                .Produces<ReasonDto>(200)
                .Produces(404);

            reasons.MapPost("/", Create)
                .WithName("CreateReason")
                .Accepts<EditReasonDto>("application/json")
                .Produces<ReasonDto>(201)
                .Produces(400);

            reasons.MapPut("/{id:int}", Update)
                .WithName("UpdateReason")
                .Accepts<EditReasonDto>("application/json")
                .Produces<ReasonDto>(200)
                .Produces(400);

            reasons.MapDelete("/{id:int}", Delete)
                .WithName("DeleteReason")
                .Produces(204)
                .Produces(400);
        }

        public static async Task<IResult> GetAll([FromServices] IUnitOfWork unitOfWork, [FromServices] IMapper mapper)
        {
            var reasons = await unitOfWork.Reasons.GetAll();
            return TypedResults.Ok(mapper.Map<IEnumerable<ReasonDto>>(reasons));
        }

        public static async Task<IResult> GetById([FromServices] IUnitOfWork unitOfWork, [FromServices] IMapper mapper, int id)
        {
            var reason = await unitOfWork.Reasons.GetById(r => r.Id == id);
            return reason != null ? TypedResults.Ok(mapper.Map<ReasonDto>(reason)) : TypedResults.NotFound("Reason not found.");
        }

        [Authorize(Roles = "Admin")]
        public static async Task<IResult> Create([FromServices] IUnitOfWork unitOfWork, [FromServices] IMapper mapper, [FromBody] EditReasonDto reasonDto)
        {
            if (reasonDto == null) return TypedResults.BadRequest("Invalid reason data.");

            var reason = mapper.Map<Reason>(reasonDto);
            await unitOfWork.Reasons.Create(reason);
            await unitOfWork.SaveAsync();

            return TypedResults.Created($"/api/reasons/{reason.Id}", mapper.Map<ReasonDto>(reason));
        }

        [Authorize(Roles = "Admin")]
        public static async Task<IResult> Update([FromServices] IUnitOfWork unitOfWork, [FromServices] IMapper mapper, int id, [FromBody] EditReasonDto reasonDto)
        {
            var reason = await unitOfWork.Reasons.GetById(r => r.Id == id);
            if (reason == null) return TypedResults.BadRequest("Invalid reason data.");

            mapper.Map(reasonDto, reason);
            unitOfWork.Reasons.Update(reason);
            await unitOfWork.SaveAsync();

            return TypedResults.Ok(mapper.Map<ReasonDto>(reason));
        }

        [Authorize(Roles = "Admin")]
        public static async Task<IResult> Delete([FromServices] IUnitOfWork unitOfWork, int id)
        {
            var reason = await unitOfWork.Reasons.GetById(r => r.Id == id);
            if (reason == null) return TypedResults.NotFound("Reason not found.");

            unitOfWork.Reasons.Delete(reason);
            await unitOfWork.SaveAsync();

            return TypedResults.NoContent();
        }
    }
}
