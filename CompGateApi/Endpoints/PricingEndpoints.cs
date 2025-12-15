using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using Microsoft.AspNetCore.Mvc;

namespace CompGateApi.Endpoints
{
    public class PricingEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var grp = app.MapGroup("/api/admin/pricing")
                         .RequireAuthorization("RequireAdminUser")
                         .WithTags("Pricing");

            grp.MapGet("/", GetAll)
               .Produces<PagedResult<PricingDto>>(200);

            grp.MapGet("/{id:int}", GetById)
               .Produces<PricingDto>(200)
               .Produces(404);

            grp.MapPost("/", Create)
               .Accepts<PricingCreateDto>("application/json")
               .Produces<PricingDto>(201)
               .Produces(400);

            grp.MapPut("/{id:int}", Update)
               .Accepts<PricingUpdateDto>("application/json")
               .Produces<PricingDto>(200)
               .Produces(404)
               .Produces(400);
            // POST alias for update
            grp.MapPost("/{id:int}/update", Update)
               .Accepts<PricingUpdateDto>("application/json")
               .Produces<PricingDto>(200)
               .Produces(404)
               .Produces(400);

            grp.MapDelete("/{id:int}", Delete)
               .Produces(204)
               .Produces(404);
            // POST alias for delete
            grp.MapPost("/{id:int}/delete", Delete)
               .Produces(204)
               .Produces(404);
        }

        public static async Task<IResult> GetAll(
            [FromServices] IPricingRepository repo,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50,
            [FromQuery] int? trxCatId = null,
            [FromQuery] string? searchTerm = null)
        {
            if (page <= 0) page = 1;
            if (limit <= 0 || limit > 500) limit = 50;

            var total = await repo.GetCountAsync(trxCatId, searchTerm);
            var items = await repo.GetAllAsync(trxCatId, searchTerm, page, limit);

            var dto = items.Select(ToDto).ToList();

            return Results.Ok(new PagedResult<PricingDto>
            {
                Data = dto,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)System.Math.Ceiling(total / (double)limit)
            });
        }

        public static async Task<IResult> GetById([FromServices] IPricingRepository repo, int id)
        {
            var entity = await repo.GetByIdAsync(id);
            return entity is null ? Results.NotFound() : Results.Ok(ToDto(entity));
        }

        public static async Task<IResult> Create([FromServices] IPricingRepository repo, [FromBody] PricingCreateDto dto)
        {
            var entity = new Pricing
            {
                TrxCatId = dto.TrxCatId,
                PctAmt = dto.PctAmt,
                Price = dto.Price,
                AmountRule = string.IsNullOrWhiteSpace(dto.AmountRule) ? null : dto.AmountRule.Trim(),
                Unit = dto.Unit,
                Description = dto.Description,

                GL1 = dto.GL1,
                GL2 = dto.GL2,
                GL3 = dto.GL3,
                GL4 = dto.GL4,

                DTC = dto.DTC,
                CTC = dto.CTC,
                DTC2 = dto.DTC2,
                CTC2 = dto.CTC2,

                NR2 = dto.NR2,
                APPLYTR2 = dto.APPLYTR2
            };

            try
            {
                var created = await repo.CreateAsync(entity);
                return Results.Created($"/api/admin/pricing/{created.Id}", ToDto(created));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        }

        public static async Task<IResult> Update([FromServices] IPricingRepository repo, int id, [FromBody] PricingUpdateDto dto)
        {
            var toUpdate = new Pricing
            {
                Id = id,
                TrxCatId = dto.TrxCatId,
                PctAmt = dto.PctAmt,
                Price = dto.Price,
                AmountRule = string.IsNullOrWhiteSpace(dto.AmountRule) ? null : dto.AmountRule.Trim(),
                Unit = dto.Unit,
                Description = dto.Description,

                GL1 = dto.GL1,
                GL2 = dto.GL2,
                GL3 = dto.GL3,
                GL4 = dto.GL4,

                DTC = dto.DTC,
                CTC = dto.CTC,
                DTC2 = dto.DTC2,
                CTC2 = dto.CTC2,

                NR2 = dto.NR2,
                APPLYTR2 = dto.APPLYTR2
            };

            try
            {
                var ok = await repo.UpdateAsync(toUpdate);
                if (!ok) return Results.NotFound();
                var fresh = await repo.GetByIdAsync(id);
                return Results.Ok(ToDto(fresh!));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        }

        public static async Task<IResult> Delete([FromServices] IPricingRepository repo, int id)
        {
            var ok = await repo.DeleteAsync(id);
            return ok ? Results.NoContent() : Results.NotFound();
        }

        private static PricingDto ToDto(Pricing p) => new()
        {
            Id = p.Id,
            TrxCatId = p.TrxCatId,
            PctAmt = p.PctAmt,
            Price = p.Price,
            AmountRule = p.AmountRule,
            Unit = p.Unit,
            Description = p.Description,

            GL1 = p.GL1,
            GL2 = p.GL2,
            GL3 = p.GL3,
            GL4 = p.GL4,

            DTC = p.DTC,
            CTC = p.CTC,
            DTC2 = p.DTC2,
            CTC2 = p.CTC2,

            NR2 = p.NR2,
            APPLYTR2 = p.APPLYTR2
        };
    }
}
