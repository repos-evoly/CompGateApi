// using CompGateApi.Abstractions;
// using CompGateApi.Core.Dtos;
// using CompGateApi.Data.Context;
// using CompGateApi.Data.Models;
// using Microsoft.AspNetCore.Builder;
// using Microsoft.AspNetCore.Http;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
// using System.Linq;
// using System.Threading.Tasks;

// namespace CompGateApi.Endpoints
// {
//     public class ServicePackageDetailEndpoints : IEndpoints
//     {
//         public void RegisterEndpoints(WebApplication app)
//         {
//             var grp = app.MapGroup("/api/servicepackages/{packageId:int}/details")
//                          .WithTags("ServicePackageDetails")
//                          .RequireAuthorization("RequireAdminUser");

//             grp.MapGet("/", GetDetailsForPackage)
//                .Produces<ServicePackageDetailDto[]>(200);

//             grp.MapPut("/{categoryId:int}", UpdateDetailForPackage)
//                .Accepts<ServicePackageDetailUpdateDto>("application/json")
//                .Produces<ServicePackageDetailDto>(200)
//                .Produces(400)
//                .Produces(404);
//         }

//         public static async Task<IResult> GetDetailsForPackage(
//             [FromRoute] int packageId,
//             [FromServices] CompGateApiDbContext db)
//         {
//             var details = await db.ServicePackageDetails
//                 .Where(d => d.ServicePackageId == packageId)
//                 .Include(d => d.TransactionCategory)
//                 .AsNoTracking()
//                 .Select(d => new ServicePackageDetailDto
//                 {
//                     TransactionCategoryId = d.TransactionCategoryId,
//                     TransactionCategoryName = d.TransactionCategory.Name,
//                     IsEnabledForPackage = d.IsEnabledForPackage
//                 })
//                 .ToListAsync();

//             return details.Any()
//                 ? Results.Ok(details)
//                 : Results.NotFound();
//         }

//         public static async Task<IResult> UpdateDetailForPackage(
//             [FromRoute] int packageId,
//             [FromRoute] int categoryId,
//             [FromBody] ServicePackageDetailUpdateDto dto,
//             [FromServices] CompGateApiDbContext db,
//             [FromServices] ILogger<ServicePackageDetailEndpoints> log)
//         {
//             var detail = await db.ServicePackageDetails
//                 .FirstOrDefaultAsync(d =>
//                     d.ServicePackageId == packageId &&
//                     d.TransactionCategoryId == categoryId);

//             if (detail == null)
//                 return Results.NotFound();

//             detail.IsEnabledForPackage = dto.IsEnabledForPackage;
//             await db.SaveChangesAsync();

//             var outDto = new ServicePackageDetailDto
//             {
//                 TransactionCategoryId = detail.TransactionCategoryId,
//                 TransactionCategoryName = (await db.TransactionCategories.FindAsync(categoryId))?.Name ?? string.Empty,
//                 IsEnabledForPackage = detail.IsEnabledForPackage
//             };

//             log.LogInformation(
//                 "Toggled category {CategoryId} for package {PackageId}: enabled={Enabled}",
//                 categoryId, packageId, detail.IsEnabledForPackage);

//             return Results.Ok(outDto);
//         }
//     }
// }