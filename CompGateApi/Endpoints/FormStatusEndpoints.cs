// CompGateApi.Endpoints/FormStatusEndpoints.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CompGateApi.Endpoints
{
    public class FormStatusEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var statuses = app
                .MapGroup("/api/formstatuses")
                .WithTags("FormStatuses")
                .RequireAuthorization("RequireCompanyUser");

            statuses.MapGet("/", GetFormStatuses)
                    .WithName("GetFormStatuses")
                    .Produces<PagedResult<FormStatusDto>>(200);

            statuses.MapGet("/{id:int}", GetFormStatusById)
                    .WithName("GetFormStatusById")
                    .Produces<FormStatusDto>(200)
                    .Produces(404);

            statuses.MapPost("/", CreateFormStatus)
                    .WithName("CreateFormStatus")
                    .Accepts<FormStatusCreateDto>("application/json")
                    .Produces<FormStatusDto>(201);

            statuses.MapPut("/{id:int}", UpdateFormStatus)
                    .WithName("UpdateFormStatus")
                    .Accepts<FormStatusUpdateDto>("application/json")
                    .Produces<FormStatusDto>(200)
                    .Produces(404);

            statuses.MapDelete("/{id:int}", DeleteFormStatus)
                    .WithName("DeleteFormStatus")
                    .Produces(200)
                    .Produces(404);
        }

        public static async Task<IResult> GetFormStatuses(
            IFormStatusRepository repo,
            ILogger<FormStatusEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 100000)
        {
            var list = await repo.GetAllAsync(searchTerm, searchBy, page, limit);
            var total = await repo.GetCountAsync(searchTerm, searchBy);

            var dtos = list.Select(fs => new FormStatusDto
            {
                Id = fs.Id,
                NameEn = fs.NameEn,
                NameAr = fs.NameAr,
                DescriptionEn = fs.DescriptionEn,
                DescriptionAr = fs.DescriptionAr,
                CreatedAt = fs.CreatedAt,
                UpdatedAt = fs.UpdatedAt
            }).ToList();

            return Results.Ok(new PagedResult<FormStatusDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalPages = (int)Math.Ceiling(total / (double)limit),
                TotalRecords = total
            });
        }

        public static async Task<IResult> GetFormStatusById(
            int id,
            IFormStatusRepository repo)
        {
            var fs = await repo.GetByIdAsync(id);
            if (fs == null)
                return Results.NotFound("FormStatus not found.");

            var dto = new FormStatusDto
            {
                Id = fs.Id,
                NameEn = fs.NameEn,
                NameAr = fs.NameAr,
                DescriptionEn = fs.DescriptionEn,
                DescriptionAr = fs.DescriptionAr,
                CreatedAt = fs.CreatedAt,
                UpdatedAt = fs.UpdatedAt
            };
            return Results.Ok(dto);
        }

        public static async Task<IResult> CreateFormStatus(
            [FromBody] FormStatusCreateDto dto,
            IFormStatusRepository repo)
        {
            var fs = new FormStatus
            {
                NameEn = dto.NameEn,
                NameAr = dto.NameAr,
                DescriptionEn = dto.DescriptionEn,
                DescriptionAr = dto.DescriptionAr
            };

            await repo.CreateAsync(fs);

            var outDto = new FormStatusDto
            {
                Id = fs.Id,
                NameEn = fs.NameEn,
                NameAr = fs.NameAr,
                DescriptionEn = fs.DescriptionEn,
                DescriptionAr = fs.DescriptionAr,
                CreatedAt = fs.CreatedAt,
                UpdatedAt = fs.UpdatedAt
            };
            return Results.Created($"/api/formstatuses/{fs.Id}", outDto);
        }

        public static async Task<IResult> UpdateFormStatus(
            int id,
            [FromBody] FormStatusUpdateDto dto,
            IFormStatusRepository repo)
        {
            var fs = await repo.GetByIdAsync(id);
            if (fs == null)
                return Results.NotFound("FormStatus not found.");

            fs.NameEn = dto.NameEn;
            fs.NameAr = dto.NameAr;
            fs.DescriptionEn = dto.DescriptionEn;
            fs.DescriptionAr = dto.DescriptionAr;

            await repo.UpdateAsync(fs);

            var outDto = new FormStatusDto
            {
                Id = fs.Id,
                NameEn = fs.NameEn,
                NameAr = fs.NameAr,
                DescriptionEn = fs.DescriptionEn,
                DescriptionAr = fs.DescriptionAr,
                CreatedAt = fs.CreatedAt,
                UpdatedAt = fs.UpdatedAt
            };
            return Results.Ok(outDto);
        }

        public static async Task<IResult> DeleteFormStatus(
            int id,
            IFormStatusRepository repo)
        {
            var fs = await repo.GetByIdAsync(id);
            if (fs == null)
                return Results.NotFound("FormStatus not found.");

            await repo.DeleteAsync(id);
            return Results.Ok("FormStatus deleted successfully.");
        }
    }
}
