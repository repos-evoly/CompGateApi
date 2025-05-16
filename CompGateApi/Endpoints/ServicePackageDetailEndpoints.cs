// ─────────────────────────────────────────────────────────────────────────────
// CompGateApi.Endpoints/ServicePackageDetailEndpoints.cs
// ─────────────────────────────────────────────────────────────────────────────
using System.Threading.Tasks;
using System.Linq;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CompGateApi.Abstractions;

namespace CompGateApi.Endpoints
{
    public class ServicePackageDetailEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var group = app
                .MapGroup("/api/service-package-details")
                .WithTags("ServicePackageDetails")
                .RequireAuthorization("RequireAdminUser")
                .RequireAuthorization("AdminAccess");

            group.MapGet("/", GetAll)
                 .WithName("GetServicePackageDetails")
                 .Produces<ServicePackageDetailDto[]>(200);

            group.MapGet("/{id:int}", GetById)
                 .WithName("GetServicePackageDetailById")
                 .Produces<ServicePackageDetailDto>(200)
                 .Produces(404);

            group.MapPost("/", Create)
                 .WithName("CreateServicePackageDetail")
                 .Accepts<ServicePackageDetailCreateDto>("application/json")
                 .Produces<ServicePackageDetailDto>(201)
                 .Produces(400);

            group.MapPut("/{id:int}", Update)
                 .WithName("UpdateServicePackageDetail")
                 .Accepts<ServicePackageDetailUpdateDto>("application/json")
                 .Produces<ServicePackageDetailDto>(200)
                 .Produces(400)
                 .Produces(404);

            group.MapDelete("/{id:int}", Delete)
                 .WithName("DeleteServicePackageDetail")
                 .Produces(200)
                 .Produces(404);
        }

        public static async Task<IResult> GetAll(
            [FromServices] IServicePackageDetailRepository repo,
            ILogger<ServicePackageDetailEndpoints> log,
            [FromQuery] int? servicePackageId,
            [FromQuery] int? transactionCategoryId)
        {
            var list = await repo.GetAllAsync(servicePackageId, transactionCategoryId);
            var dtos = list.Select(d => new ServicePackageDetailDto
            {
                Id = d.Id,
                ServicePackageId = d.ServicePackageId,
                TransactionCategoryId = d.TransactionCategoryId,
                CommissionPct = d.CommissionPct,
                FeeFixed = d.FeeFixed
            });
            return Results.Ok(dtos);
        }

        public static async Task<IResult> GetById(
            int id,
            [FromServices] IServicePackageDetailRepository repo)
        {
            var d = await repo.GetByIdAsync(id);
            if (d == null) return Results.NotFound("Not found");
            var dto = new ServicePackageDetailDto
            {
                Id = d.Id,
                ServicePackageId = d.ServicePackageId,
                TransactionCategoryId = d.TransactionCategoryId,
                CommissionPct = d.CommissionPct,
                FeeFixed = d.FeeFixed
            };
            return Results.Ok(dto);
        }

        public static async Task<IResult> Create(
            [FromBody] ServicePackageDetailCreateDto dto,
            [FromServices] IServicePackageDetailRepository repo,
            [FromServices] IValidator<ServicePackageDetailCreateDto> validator,
            ILogger<ServicePackageDetailEndpoints> log)
        {
            var res = await validator.ValidateAsync(dto);
            if (!res.IsValid)
                return Results.BadRequest(res.Errors.Select(e => e.ErrorMessage));

            var ent = new ServicePackageDetail
            {
                ServicePackageId = dto.ServicePackageId,
                TransactionCategoryId = dto.TransactionCategoryId,
                CommissionPct = dto.CommissionPct,
                FeeFixed = dto.FeeFixed
            };
            await repo.CreateAsync(ent);

            var outDto = new ServicePackageDetailDto
            {
                Id = ent.Id,
                ServicePackageId = ent.ServicePackageId,
                TransactionCategoryId = ent.TransactionCategoryId,
                CommissionPct = ent.CommissionPct,
                FeeFixed = ent.FeeFixed
            };
            return Results.Created($"/api/service-package-details/{ent.Id}", outDto);
        }

        public static async Task<IResult> Update(
            int id,
            [FromBody] ServicePackageDetailUpdateDto dto,
            [FromServices] IServicePackageDetailRepository repo,
            [FromServices] IValidator<ServicePackageDetailUpdateDto> validator,
            ILogger<ServicePackageDetailEndpoints> log)
        {
            var ent = await repo.GetByIdAsync(id);
            if (ent == null) return Results.NotFound("Not found");

            var res = await validator.ValidateAsync(dto);
            if (!res.IsValid)
                return Results.BadRequest(res.Errors.Select(e => e.ErrorMessage));

            ent.CommissionPct = dto.CommissionPct;
            ent.FeeFixed = dto.FeeFixed;
            await repo.UpdateAsync(ent);

            var outDto = new ServicePackageDetailDto
            {
                Id = ent.Id,
                ServicePackageId = ent.ServicePackageId,
                TransactionCategoryId = ent.TransactionCategoryId,
                CommissionPct = ent.CommissionPct,
                FeeFixed = ent.FeeFixed
            };
            return Results.Ok(outDto);
        }

        public static async Task<IResult> Delete(
            int id,
            [FromServices] IServicePackageDetailRepository repo)
        {
            var ent = await repo.GetByIdAsync(id);
            if (ent == null) return Results.NotFound("Not found");
            await repo.DeleteAsync(id);
            return Results.Ok();
        }
    }
}
