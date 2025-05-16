using System;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CompGateApi.Endpoints
{
    public class TransactionCategoryEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var grp = app.MapGroup("/api/transactioncategories")
                         .WithTags("TransactionCategories")
                         .RequireAuthorization("RequireAdminUser")      // only admins manage categories
                         .RequireAuthorization("AdminAccess");

            grp.MapGet("/", GetAll)
               .WithName("GetTransactionCategories")
               .Produces<PagedResult<TransactionCategoryDto>>(200);

            grp.MapGet("/{id:int}", GetById)
               .WithName("GetTransactionCategoryById")
               .Produces<TransactionCategoryDto>(200)
               .Produces(404);

            grp.MapPost("/", Create)
               .WithName("CreateTransactionCategory")
               .Accepts<TransactionCategoryCreateDto>("application/json")
               .Produces<TransactionCategoryDto>(201)
               .Produces(400);

            grp.MapPut("/{id:int}", Update)
               .WithName("UpdateTransactionCategory")
               .Accepts<TransactionCategoryUpdateDto>("application/json")
               .Produces<TransactionCategoryDto>(200)
               .Produces(400)
               .Produces(404);

            grp.MapDelete("/{id:int}", Delete)
               .WithName("DeleteTransactionCategory")
               .Produces(200)
               .Produces(404);
        }

        public static async Task<IResult> GetAll(
            [FromServices] ITransactionCategoryRepository repo,
            [FromServices] ILogger<TransactionCategoryEndpoints> log,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            log.LogInformation("Listing categories search='{Search}' page={Page} limit={Limit}",
                search, page, limit);

            var list = await repo.GetAllAsync(search, page, limit);
            var total = await repo.GetCountAsync(search);

            var dtos = list.Select(c => new TransactionCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description
            }).ToList();

            return Results.Ok(new PagedResult<TransactionCategoryDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalPages = (int)Math.Ceiling(total / (double)limit),
                TotalRecords = total,
                
            });
        }

        public static async Task<IResult> GetById(
            int id,
            [FromServices] ITransactionCategoryRepository repo)
        {
            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound("TransactionCategory not found.");

            return Results.Ok(new TransactionCategoryDto
            {
                Id = ent.Id,
                Name = ent.Name,
                Description = ent.Description
            });
        }

        public static async Task<IResult> Create(
            [FromBody] TransactionCategoryCreateDto dto,
            [FromServices] ITransactionCategoryRepository repo,
            [FromServices] IValidator<TransactionCategoryCreateDto> validator,
            [FromServices] ILogger<TransactionCategoryEndpoints> log)
        {
            var res = await validator.ValidateAsync(dto);
            if (!res.IsValid)
                return Results.BadRequest(res.Errors.Select(e => e.ErrorMessage));

            var ent = new TransactionCategory
            {
                Name = dto.Name,
                Description = dto.Description
            };
            await repo.CreateAsync(ent);
            log.LogInformation("Created TransactionCategory Id={Id}", ent.Id);

            var outDto = new TransactionCategoryDto
            {
                Id = ent.Id,
                Name = ent.Name,
                Description = ent.Description
            };
            return Results.Created($"/api/transactioncategories/{ent.Id}", outDto);
        }

        public static async Task<IResult> Update(
            int id,
            [FromBody] TransactionCategoryUpdateDto dto,
            [FromServices] ITransactionCategoryRepository repo,
            [FromServices] IValidator<TransactionCategoryUpdateDto> validator,
            [FromServices] ILogger<TransactionCategoryEndpoints> log)
        {
            var ent = await repo.GetByIdAsync(id);
            if (ent == null) return Results.NotFound("TransactionCategory not found.");

            var res = await validator.ValidateAsync(dto);
            if (!res.IsValid)
                return Results.BadRequest(res.Errors.Select(e => e.ErrorMessage));

            ent.Name = dto.Name;
            ent.Description = dto.Description;
            await repo.UpdateAsync(ent);
            log.LogInformation("Updated TransactionCategory Id={Id}", id);

            return Results.Ok(new TransactionCategoryDto
            {
                Id = ent.Id,
                Name = ent.Name,
                Description = ent.Description
            });
        }

        public static async Task<IResult> Delete(
            int id,
            [FromServices] ITransactionCategoryRepository repo,
            [FromServices] ILogger<TransactionCategoryEndpoints> log)
        {
            var ent = await repo.GetByIdAsync(id);
            if (ent == null) return Results.NotFound("TransactionCategory not found.");

            await repo.DeleteAsync(id);
            log.LogInformation("Deleted TransactionCategory Id={Id}", id);
            return Results.Ok($"Deleted category {id}");
        }
    }
}
