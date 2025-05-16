// CompGateApi.Endpoints/TransferLimitEndpoints.cs
using System.Threading.Tasks;
using System.Linq;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CompGateApi.Abstractions;

namespace CompGateApi.Endpoints
{
    public class TransferLimitEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var group = app.MapGroup("/api/transferlimits")
                           .WithTags("TransferLimits")
                           .RequireAuthorization("RequireAdminUser")
                           .RequireAuthorization("AdminAccess");

            group.MapGet("/", GetAll)
                 .WithName("GetTransferLimits")
                 .Produces<TransferLimitDto[]>(200);

            group.MapGet("/{id:int}", GetById)
                 .WithName("GetTransferLimitById")
                 .Produces<TransferLimitDto>(200)
                 .Produces(404);

            group.MapPost("/", Create)
                 .WithName("CreateTransferLimit")
                 .Accepts<TransferLimitCreateDto>("application/json")
                 .Produces<TransferLimitDto>(201)
                 .Produces(400);

            group.MapPut("/{id:int}", Update)
                 .WithName("UpdateTransferLimit")
                 .Accepts<TransferLimitUpdateDto>("application/json")
                 .Produces<TransferLimitDto>(200)
                 .Produces(404)
                 .Produces(400);

            group.MapDelete("/{id:int}", Delete)
                 .WithName("DeleteTransferLimit")
                 .Produces(204)
                 .Produces(404);
        }

        public static async Task<IResult> GetAll(
            [FromServices] ITransferLimitRepository repo,
            [FromServices] ILogger<TransferLimitEndpoints> log,
            [FromQuery] int? servicePackageId,
            [FromQuery] int? transactionCategoryId,
            [FromQuery] int? currencyId,
            [FromQuery] string? period)
        {
            var list = await repo.GetAllAsync(servicePackageId, transactionCategoryId, currencyId, period);
            var dtos = list.Select(l => new TransferLimitDto
            {
                Id = l.Id,
                ServicePackageId = l.ServicePackageId,
                TransactionCategoryId = l.TransactionCategoryId,
                CurrencyId = l.CurrencyId,
                Period = l.Period.ToString(),
                MinAmount = l.MinAmount,
                MaxAmount = l.MaxAmount,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt
            }).ToArray();

            log.LogInformation("Returned {Count} transfer-limits", dtos.Length);
            return Results.Ok(dtos);
        }

        public static async Task<IResult> GetById(
            int id,
            [FromServices] ITransferLimitRepository repo)
        {
            var l = await repo.GetByIdAsync(id);
            if (l == null) return Results.NotFound("Not found");

            var dto = new TransferLimitDto
            {
                Id = l.Id,
                ServicePackageId = l.ServicePackageId,
                TransactionCategoryId = l.TransactionCategoryId,
                CurrencyId = l.CurrencyId,
                Period = l.Period.ToString(),
                MinAmount = l.MinAmount,
                MaxAmount = l.MaxAmount,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt
            };
            return Results.Ok(dto);
        }

        public static async Task<IResult> Create(
            [FromBody] TransferLimitCreateDto dto,
            [FromServices] ITransferLimitRepository repo)
        {
            // Basic shape validation
            if (dto.MinAmount < 0 || dto.MaxAmount <= 0 || dto.MinAmount > dto.MaxAmount)
                return Results.BadRequest("Invalid min/max amounts.");

            var ent = new TransferLimit
            {
                ServicePackageId = dto.ServicePackageId,
                TransactionCategoryId = dto.TransactionCategoryId,
                CurrencyId = dto.CurrencyId,
                Period = Enum.Parse<LimitPeriod>(dto.Period, true),
                MinAmount = dto.MinAmount,
                MaxAmount = dto.MaxAmount
            };

            await repo.CreateAsync(ent);

            var outDto = new TransferLimitDto
            {
                Id = ent.Id,
                ServicePackageId = ent.ServicePackageId,
                TransactionCategoryId = ent.TransactionCategoryId,
                CurrencyId = ent.CurrencyId,
                Period = ent.Period.ToString(),
                MinAmount = ent.MinAmount,
                MaxAmount = ent.MaxAmount,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };

            return Results.Created($"/api/transferlimits/{ent.Id}", outDto);
        }

        public static async Task<IResult> Update(
            int id,
            [FromBody] TransferLimitUpdateDto dto,
            [FromServices] ITransferLimitRepository repo)
        {
            var ent = await repo.GetByIdAsync(id);
            if (ent == null) return Results.NotFound("Not found");

            if (dto.MinAmount < 0 || dto.MaxAmount <= 0 || dto.MinAmount > dto.MaxAmount)
                return Results.BadRequest("Invalid min/max amounts.");

            ent.MinAmount = dto.MinAmount;
            ent.MaxAmount = dto.MaxAmount;
            await repo.UpdateAsync(ent);

            var outDto = new TransferLimitDto
            {
                Id = ent.Id,
                ServicePackageId = ent.ServicePackageId,
                TransactionCategoryId = ent.TransactionCategoryId,
                CurrencyId = ent.CurrencyId,
                Period = ent.Period.ToString(),
                MinAmount = ent.MinAmount,
                MaxAmount = ent.MaxAmount,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Ok(outDto);
        }

        public static async Task<IResult> Delete(
            int id,
            [FromServices] ITransferLimitRepository repo)
        {
            var ent = await repo.GetByIdAsync(id);
            if (ent == null) return Results.NotFound("Not found");
            await repo.DeleteAsync(id);
            return Results.NoContent();
        }
    }
}
