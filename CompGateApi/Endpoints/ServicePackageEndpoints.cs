// CompGateApi.Endpoints/ServicePackageEndpoints.cs
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
    public class ServicePackageEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var grp = app.MapGroup("/api/servicepackages")
                         .WithTags("ServicePackages")
                         .RequireAuthorization("RequireAdminUser")
                         .RequireAuthorization("AdminAccess");

            grp.MapGet("/", GetAll)
               .Produces<ServicePackageListDto[]>(200);

            grp.MapGet("/{id:int}", GetById)
               .Produces<ServicePackageDetailsDto>(200)
               .Produces(404);

            grp.MapGet("/{id:int}/categories/{categoryId:int}", GetCategoryDetail)
           .Produces<ServicePackageCategoryDto>(200)
           .Produces(404);

            grp.MapPut("/{id:int}/categories/{categoryId:int}", UpdateCategoryDetail)
               .Accepts<ServicePackageCategoryUpdateDto>("application/json")
               .Produces<ServicePackageCategoryDto>(200)
               .Produces(400)
               .Produces(404);

            grp.MapPost("/", Create)
               .Accepts<ServicePackageCreateDto>("application/json")
               .Produces<ServicePackageListDto>(201)
               .Produces(400);

            grp.MapPut("/{id:int}", Update)
               .Accepts<ServicePackageUpdateDto>("application/json")
               .Produces<ServicePackageListDto>(200)
               .Produces(400)
               .Produces(404);

            grp.MapDelete("/{id:int}", Delete)
               .Produces(204)
               .Produces(404);
        }

        // Returns the same DTO shape as the old GET /api/servicepackages
        public static async Task<IResult> GetAll(
            IServicePackageRepository repo,
            CompGateApiDbContext db,
            ILogger<ServicePackageEndpoints> log)
        {
            log.LogInformation("Fetching all service packages");

            // load all packages
            var pkgs = await repo.GetAllAsync();
            // pre-fetch details for all packages in one go
            var allDetails = await db.ServicePackageDetails
                .Include(d => d.TransactionCategory)
                .AsNoTracking()
                .ToListAsync();

            var dtos = pkgs.Select(p =>
            {
                var details = allDetails
                    .Where(d => d.ServicePackageId == p.Id)
                    .Select(d => new ServicePackageCategoryDto
                    {
                        TransactionCategoryId = d.TransactionCategoryId,
                        TransactionCategoryName = d.TransactionCategory.Name,
                        TransactionCategoryHasLimits = d.TransactionCategory.HasLimits,
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
                    .ToList();

                return new ServicePackageListDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    DailyLimit = p.DailyLimit,
                    MonthlyLimit = p.MonthlyLimit,
                    Categories = details
                };
            })
            .ToArray();

            return Results.Ok(dtos);
        }

        // Returns a single package by id, same DTO shape as old GET /api/servicepackages/{id}
        public static async Task<IResult> GetById(
            int id,
            IServicePackageRepository repo,
            CompGateApiDbContext db,
            ILogger<ServicePackageEndpoints> log)
        {
            log.LogInformation("Fetching service package {Id}", id);

            var pkg = await repo.GetByIdAsync(id);
            if (pkg == null) return Results.NotFound();

            var details = await db.ServicePackageDetails
                .Include(d => d.TransactionCategory)
                .Where(d => d.ServicePackageId == id)
                .AsNoTracking()
                .ToListAsync();

            var dto = new ServicePackageDetailsDto
            {
                Id = pkg.Id,
                Name = pkg.Name,
                Description = pkg.Description,
                DailyLimit = pkg.DailyLimit,
                MonthlyLimit = pkg.MonthlyLimit,
                Categories = details.Select(d => new ServicePackageCategoryDto
                {
                    TransactionCategoryId = d.TransactionCategoryId,
                    TransactionCategoryName = d.TransactionCategory.Name,
                    TransactionCategoryHasLimits = d.TransactionCategory.HasLimits,
                    IsEnabledForPackage = d.IsEnabledForPackage,
                    B2BTransactionLimit = d.B2BTransactionLimit,
                    B2CTransactionLimit = d.B2CTransactionLimit,
                    B2BFixedFee = d.B2BFixedFee,
                    B2CFixedFee = d.B2CFixedFee,
                    B2BMinPercentage = d.B2BMinPercentage,
                    B2CMinPercentage = d.B2CMinPercentage,

                    B2BCommissionPct = d.B2BCommissionPct,

                    B2CCommissionPct = d.B2CCommissionPct
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
                Description = dto.Description,
                DailyLimit = dto.DailyLimit,
                MonthlyLimit = dto.MonthlyLimit
            };

            await repo.CreateAsync(pkg);
            log.LogInformation("Created service package {Id}", pkg.Id);

            // return exactly same shape as GetAll
            var outDto = new ServicePackageListDto
            {
                Id = pkg.Id,
                Name = pkg.Name,
                Description = pkg.Description,
                DailyLimit = pkg.DailyLimit,
                MonthlyLimit = pkg.MonthlyLimit,
                Categories = new List<ServicePackageCategoryDto>()  // no details yet
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
            pkg.DailyLimit = dto.DailyLimit;
            pkg.MonthlyLimit = dto.MonthlyLimit;

            await repo.UpdateAsync(pkg);
            log.LogInformation("Updated service package {Id}", id);

            var outDto = new ServicePackageListDto
            {
                Id = pkg.Id,
                Name = pkg.Name,
                Description = pkg.Description,
                DailyLimit = pkg.DailyLimit,
                MonthlyLimit = pkg.MonthlyLimit,
                Categories = new List<ServicePackageCategoryDto>()
            };

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

        public static async Task<IResult> GetCategoryDetail(
            [FromRoute] int id,
            [FromRoute] int categoryId,
            [FromServices] CompGateApiDbContext db,
            [FromServices] ILogger<ServicePackageEndpoints> log)
        {
            log.LogInformation("Fetching package {Pkg} category detail {Cat}", id, categoryId);

            var d = await db.ServicePackageDetails
                            .Include(x => x.TransactionCategory)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x =>
                                x.ServicePackageId == id &&
                                x.TransactionCategoryId == categoryId);

            if (d == null) return Results.NotFound();

            var dto = new ServicePackageCategoryDto
            {
                ServicePackageId = d.ServicePackageId,
                ServicePackageName = (await db.ServicePackages.FindAsync(id))?.Name ?? "",
                TransactionCategoryId = d.TransactionCategoryId,
                TransactionCategoryName = d.TransactionCategory.Name,
                TransactionCategoryHasLimits = d.TransactionCategory.HasLimits,
                IsEnabledForPackage = d.IsEnabledForPackage,
                B2BTransactionLimit = d.B2BTransactionLimit,
                B2CTransactionLimit = d.B2CTransactionLimit,
                B2BFixedFee = d.B2BFixedFee,
                B2CFixedFee = d.B2CFixedFee,
                B2BMinPercentage = d.B2BMinPercentage,
                B2CMinPercentage = d.B2CMinPercentage,

                B2BCommissionPct = d.B2BCommissionPct,

                B2CCommissionPct = d.B2CCommissionPct
            };

            return Results.Ok(dto);
        }

        // ─── Update one category detail ──────────────────────────────
        public static async Task<IResult> UpdateCategoryDetail(
            [FromRoute] int id,
            [FromRoute] int categoryId,
            [FromBody] ServicePackageCategoryUpdateDto dto,
            [FromServices] CompGateApiDbContext db,
            [FromServices] ILogger<ServicePackageEndpoints> log)
        {
            var d = await db.ServicePackageDetails
                            .FirstOrDefaultAsync(x =>
                                x.ServicePackageId == id &&
                                x.TransactionCategoryId == categoryId);

            if (d == null) return Results.NotFound();

            // apply updates
            d.IsEnabledForPackage = dto.IsEnabledForPackage;
            d.B2BTransactionLimit = dto.B2BTransactionLimit;
            d.B2CTransactionLimit = dto.B2CTransactionLimit;
            d.B2BFixedFee = dto.B2BFixedFee;
            d.B2CFixedFee = dto.B2CFixedFee;
            d.B2BMinPercentage = dto.B2BMinPercentage;
            d.B2CMinPercentage = dto.B2CMinPercentage;

            d.B2BCommissionPct = dto.B2BCommissionPct;

            d.B2CCommissionPct = dto.B2CCommissionPct;

            await db.SaveChangesAsync();

            var outDto = new ServicePackageCategoryDto
            {
                TransactionCategoryId = d.TransactionCategoryId,
                TransactionCategoryName = (await db.TransactionCategories.FindAsync(categoryId))?.Name ?? "",
                TransactionCategoryHasLimits = (await db.TransactionCategories.FindAsync(categoryId))?.HasLimits ?? false,
                IsEnabledForPackage = d.IsEnabledForPackage,
                B2BTransactionLimit = d.B2BTransactionLimit,
                B2CTransactionLimit = d.B2CTransactionLimit,
                B2BFixedFee = d.B2BFixedFee,
                B2CFixedFee = d.B2CFixedFee,
                B2BMinPercentage = d.B2BMinPercentage,
                B2CMinPercentage = d.B2CMinPercentage,
                B2BCommissionPct = d.B2BCommissionPct,
                B2CCommissionPct = d.B2CCommissionPct
            };
            log.LogInformation("Updated package {Pkg} category detail {Cat}", id, categoryId);
            return Results.Ok(outDto);
        }
    }
}
