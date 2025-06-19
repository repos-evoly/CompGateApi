// CompGateApi.Endpoints/TransactionCategoryEndpoints.cs
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace CompGateApi.Endpoints
{
    public class TransactionCategoryEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var grp = app.MapGroup("/api/transactioncategories")
                         .WithTags("TransactionCategories")
                         .RequireAuthorization("RequireAdminUser");

            grp.MapGet("/", GetAll)
               .Produces<TransactionCategoryDto[]>(200);

            grp.MapGet("/by-package/{packageId:int}", GetByServicePackage)
               .Produces<TransactionCategoryByPackageDto[]>(200);

            grp.MapPost("/", Create)
               .Accepts<TransactionCategoryCreateDto>("application/json")
               .Produces<TransactionCategoryDto>(201)
               .Produces(400);

            grp.MapGet("/{id:int}", GetById)
                .Produces<TransactionCategoryDto>(200)
                .Produces(404);

            grp.MapPut("/{id:int}", Update)
               .Accepts<TransactionCategoryUpdateDto>("application/json")
               .Produces<TransactionCategoryDto>(200)
               .Produces(400)
               .Produces(404);

            grp.MapDelete("/{id:int}", Delete)
               .Produces(204)
               .Produces(404);
        }

        // GET /api/transactioncategories
        public static async Task<IResult> GetAll(
            [FromServices] ITransactionCategoryRepository repo,
            [FromServices] ILogger<TransactionCategoryEndpoints> log)
        {
            log.LogInformation("Fetching all transaction categories");
            var cats = await repo.GetAllAsync();
            var dtos = cats
                .Select(c => new TransactionCategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    HasLimits = c.HasLimits
                })
                .ToArray();

            return Results.Ok(dtos);
        }

        // GET /api/transactioncategories/by-package/5
        public static async Task<IResult> GetByServicePackage(
            [FromRoute] int packageId,
            [FromServices] ITransactionCategoryRepository repo,
            [FromServices] ILogger<TransactionCategoryEndpoints> log)
        {
            log.LogInformation("Fetching categories for service package {PackageId}", packageId);

            // pull the per-package join rows (which include the ranges & fees)
            var details = await repo.GetByServicePackageAsync(packageId);
            var dtos = details
                .Select(d => new TransactionCategoryByPackageDto
                {
                    Id = d.TransactionCategoryId,
                    Name = d.TransactionCategory.Name,
                    HasLimits = d.TransactionCategory.HasLimits,

                    IsEnabledForPackage = d.IsEnabledForPackage,
                    B2BTransactionLimit = d.B2BTransactionLimit,
                    B2CTransactionLimit = d.B2CTransactionLimit,
                    B2BFixedFee = d.B2BFixedFee,
                    B2CFixedFee = d.B2CFixedFee,
                    B2BMinPercentage = d.B2BMinPercentage,
                    B2CMinPercentage = d.B2CMinPercentage,

                    B2BCommissionPct = d.B2BCommissionPct,

                    B2CCommissionPct = d.B2CCommissionPct
                })
                .ToArray();

            return Results.Ok(dtos);
        }

        // POST /api/transactioncategories
        public static async Task<IResult> Create(
    [FromBody] TransactionCategoryCreateDto dto,
    [FromServices] ITransactionCategoryRepository repo,
    [FromServices] CompGateApiDbContext db,   // add DbContext
    [FromServices] ILogger<TransactionCategoryEndpoints> log)
        {
            // 1) Create the category
            var cat = new TransactionCategory { Name = dto.Name };
            await repo.CreateAsync(cat);
            log.LogInformation("Created category {Id}", cat.Id);

            // 2) If they provided a package ID, link them
            if (dto.ServicePackageId.HasValue)
            {
                // verify package exists
                var pkg = await db.ServicePackages.FindAsync(dto.ServicePackageId.Value);
                if (pkg == null)
                    return Results.BadRequest($"Package {dto.ServicePackageId.Value} not found.");

                var detail = new ServicePackageDetail
                {
                    ServicePackageId = dto.ServicePackageId.Value,
                    TransactionCategoryId = cat.Id,
                    IsEnabledForPackage = dto.IsEnabledForPackage ?? true,
                    B2BTransactionLimit = dto.B2BTransactionLimit ?? 0m,
                    B2CTransactionLimit = dto.B2CTransactionLimit ?? 0m,
                    B2BFixedFee = dto.B2BFixedFee ?? 0m,
                    B2CFixedFee = dto.B2CFixedFee ?? 0m,
                    B2BMinPercentage = dto.B2BMinPercentage ?? 0m,
                    B2CMinPercentage = dto.B2CMinPercentage ?? 0m,

                    B2BCommissionPct = dto.B2BCommissionPct ?? 0m,

                    B2CCommissionPct = dto.B2CCommissionPct ?? 0m
                };
                db.ServicePackageDetails.Add(detail);
                await db.SaveChangesAsync();
                log.LogInformation("Linked category {Cat} to package {Pkg}", cat.Id, pkg.Id);
            }

            // 3) Return simple DTO
            var outDto = new TransactionCategoryDto { Id = cat.Id, Name = cat.Name };
            return Results.Created($"/api/transactioncategories/{cat.Id}", outDto);
        }

        public static async Task<IResult> GetById(
    [FromRoute] int id,
    [FromServices] ITransactionCategoryRepository repo,
    [FromServices] ILogger<TransactionCategoryEndpoints> log)
        {
            log.LogInformation("Fetching transaction category {Id}", id);
            var cat = await repo.GetByIdAsync(id);
            if (cat == null)
                return Results.NotFound();

            var dto = new TransactionCategoryDto
            {
                Id = cat.Id,
                Name = cat.Name,
                HasLimits = cat.HasLimits  // ← if you want to expose this
            };
            return Results.Ok(dto);
        }

        // PUT /api/transactioncategories/5
        public static async Task<IResult> Update(
            [FromRoute] int id,
            [FromBody] TransactionCategoryUpdateDto dto,
            [FromServices] ITransactionCategoryRepository repo,
            // [FromServices] IValidator<TransactionCategoryUpdateDto> validator,
            [FromServices] ILogger<TransactionCategoryEndpoints> log)
        {
            // var validation = await validator.ValidateAsync(dto);
            // if (!validation.IsValid)
            //     return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));

            var cat = await repo.GetByIdAsync(id);
            if (cat == null) return Results.NotFound();

            cat.Name = dto.Name;
            await repo.UpdateAsync(cat);

            log.LogInformation("Updated transaction category {Id}", id);
            return Results.Ok(new TransactionCategoryDto
            {
                Id = cat.Id,
                Name = cat.Name,
                HasLimits = cat.HasLimits
            });
        }

        // DELETE /api/transactioncategories/5
        public static async Task<IResult> Delete(
            [FromRoute] int id,
            [FromServices] ITransactionCategoryRepository repo,
            [FromServices] ILogger<TransactionCategoryEndpoints> log)
        {
            var cat = await repo.GetByIdAsync(id);
            if (cat == null) return Results.NotFound();

            await repo.DeleteAsync(id);
            log.LogInformation("Deleted transaction category {Id}", id);
            return Results.NoContent();
        }
    }
}
