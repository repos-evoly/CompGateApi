// ─────────────────────────────────────────────────────────────────────────────
// CompGateApi.Endpoints/ServicePackageEndpoints.cs
// ─────────────────────────────────────────────────────────────────────────────

using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace CompGateApi.Endpoints
{
    public class ServicePackageEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var grp = app.MapGroup("/api/servicepackages")
                         .WithTags("ServicePackages")
                         .RequireAuthorization("RequireAdminUser")    
                         .RequireAuthorization("AdminAccess");

            grp.MapGet("/", GetAll)
               .Produces<ServicePackageDto[]>(200);

            grp.MapGet("/{id:int}", GetById)
               .Produces<ServicePackageDto>(200)
               .Produces(404);

            grp.MapPost("/", Create)
               .Accepts<ServicePackageCreateDto>("application/json")
               .Produces<ServicePackageDto>(201)
               .Produces(400);

            grp.MapPut("/{id:int}", Update)
               .Accepts<ServicePackageUpdateDto>("application/json")
               .Produces<ServicePackageDto>(200)
               .Produces(400)
               .Produces(404);

            grp.MapDelete("/{id:int}", Delete)
               .Produces(204)
               .Produces(404);
        }

        public static async Task<IResult> GetAll(
            IServicePackageRepository repo,
            ILogger<ServicePackageEndpoints> log)
        {
            log.LogInformation("Fetching all service packages");
            var pkgs = await repo.GetAllAsync();
            var dtos = pkgs.Select(p => new ServicePackageDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Details = p.ServicePackageDetails.Select(d => new ServicePackageDetailDto
                {
                    Id = d.Id,
                    TransactionCategoryId = d.TransactionCategoryId,
                    CommissionPct = d.CommissionPct,
                    FeeFixed = d.FeeFixed
                }).ToList(),
                Limits = p.TransferLimits.Select(l => new TransferLimitDto
                {
                    Id = l.Id,
                    TransactionCategoryId = l.TransactionCategoryId,
                    CurrencyId = l.CurrencyId,
                    Period = l.Period.ToString(),
                    MinAmount = l.MinAmount,
                    MaxAmount = l.MaxAmount
                }).ToList()
            }).ToArray();

            return Results.Ok(dtos);
        }

        public static async Task<IResult> GetById(
            int id,
            IServicePackageRepository repo,
            ILogger<ServicePackageEndpoints> log)
        {
            log.LogInformation("Fetching service package {Id}", id);
            var p = await repo.GetByIdAsync(id);
            if (p == null) return Results.NotFound();
            var dto = new ServicePackageDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Details = p.ServicePackageDetails.Select(d => new ServicePackageDetailDto
                {
                    Id = d.Id,
                    TransactionCategoryId = d.TransactionCategoryId,
                    CommissionPct = d.CommissionPct,
                    FeeFixed = d.FeeFixed
                }).ToList(),
                Limits = p.TransferLimits.Select(l => new TransferLimitDto
                {
                    Id = l.Id,
                    TransactionCategoryId = l.TransactionCategoryId,
                    CurrencyId = l.CurrencyId,
                    Period = l.Period.ToString(),
                    MinAmount = l.MinAmount,
                    MaxAmount = l.MaxAmount
                }).ToList()
            };
            return Results.Ok(dto);
        }

        public static async Task<IResult> Create(
            ServicePackageCreateDto dto,
            IServicePackageRepository repo,
            IValidator<ServicePackageCreateDto> validator,
            ILogger<ServicePackageEndpoints> log)
        {
            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));

            var pkg = new ServicePackage
            {
                Name = dto.Name,
                Description = dto.Description
            };
            await repo.CreateAsync(pkg);
            log.LogInformation("Created service package {Id}", pkg.Id);

            var outDto = new ServicePackageDto
            {
                Id = pkg.Id,
                Name = pkg.Name,
                Description = pkg.Description
            };
            return Results.Created($"/api/servicepackages/{pkg.Id}", outDto);
        }

        public static async Task<IResult> Update(
            int id,
            ServicePackageUpdateDto dto,
            IServicePackageRepository repo,
            IValidator<ServicePackageUpdateDto> validator,
            ILogger<ServicePackageEndpoints> log)
        {
            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));

            var pkg = await repo.GetByIdAsync(id);
            if (pkg == null) return Results.NotFound();

            pkg.Name = dto.Name;
            pkg.Description = dto.Description;
            await repo.UpdateAsync(pkg);

            var outDto = new ServicePackageDto
            {
                Id = pkg.Id,
                Name = pkg.Name,
                Description = pkg.Description
            };
            log.LogInformation("Updated service package {Id}", id);
            return Results.Ok(outDto);
        }

        public static async Task<IResult> Delete(
            int id,
            IServicePackageRepository repo,
            ILogger<ServicePackageEndpoints> log)
        {
            var pkg = await repo.GetByIdAsync(id);
            if (pkg == null) return Results.NotFound();
            await repo.DeleteAsync(id);
            log.LogInformation("Deleted service package {Id}", id);
            return Results.NoContent();
        }
    }
}
