// CompGateApi.Endpoints/BeneficiaryEndpoints.cs
using System.Security.Claims;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CompGateApi.Endpoints
{
    public class BeneficiaryEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var company = app.MapGroup("/api/beneficiaries")
                            .WithTags("Beneficiaries")
                            .RequireAuthorization("RequireCompanyUser");

            company.MapGet("/", GetCompanyBeneficiaries)
                   .Produces<List<BeneficiaryDto>>(200);

            company.MapGet("/{id:int}", GetCompanyBeneficiaryById)
                   .Produces<BeneficiaryDto>(200)
                   .Produces(404);

            company.MapPost("/", CreateCompanyBeneficiary)
                   .Accepts<BeneficiaryCreateDto>("application/json")
                   .Produces<BeneficiaryDto>(201)
                   .Produces(400);

            company.MapDelete("/{id:int}", DeleteCompanyBeneficiary)
                   .Produces(204)
                   .Produces(404);
        }

        static int GetAuthUserId(HttpContext ctx)
        {
            var raw = ctx.User.FindFirst("nameid")?.Value
                   ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(raw, out var id)) return id;
            throw new UnauthorizedAccessException("Missing/invalid 'nameid' claim.");
        }

        public static async Task<IResult> GetCompanyBeneficiaries(
            HttpContext ctx,
            IBeneficiaryRepository repo,
            IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            var list = await repo.GetAllByCompanyAsync(me.CompanyId.Value);
            return Results.Ok(list.Select(b => new BeneficiaryDto
            {
                Id = b.Id,
                Type = b.Type,
                Name = b.Name,
                AccountNumber = b.AccountNumber,
                Address = b.Address,
                Country = b.Country,
                Bank = b.Bank,
                Amount = b.Amount,
                IntermediaryBankName = b.IntermediaryBankName,
                IntermediaryBankSwift = b.IntermediaryBankSwift,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt
            }));
        }

        public static async Task<IResult> GetCompanyBeneficiaryById(
            int id,
            HttpContext ctx,
            IBeneficiaryRepository repo,
            IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            var b = await repo.GetByIdAsync(id);
            if (b == null || b.CompanyId != me.CompanyId.Value || b.IsDeleted)
                return Results.NotFound();

            return Results.Ok(new BeneficiaryDto
            {
                Id = b.Id,
                Type = b.Type,
                Name = b.Name,
                AccountNumber = b.AccountNumber,
                Address = b.Address,
                Country = b.Country,
                Bank = b.Bank,
                Amount = b.Amount,
                IntermediaryBankName = b.IntermediaryBankName,
                IntermediaryBankSwift = b.IntermediaryBankSwift,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt
            });
        }

        public static async Task<IResult> CreateCompanyBeneficiary(
            [FromBody] BeneficiaryCreateDto dto,
            HttpContext ctx,
            [FromServices] IBeneficiaryRepository repo,
            [FromServices] IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            var entity = new Beneficiary
            {
                CompanyId = me.CompanyId.Value,
                Type = dto.Type,
                Name = dto.Name,
                AccountNumber = dto.AccountNumber,
                Address = dto.Address,
                Country = dto.Country,
                Bank = dto.Bank,
                Amount = dto.Amount,
                IntermediaryBankName = dto.IntermediaryBankName,
                IntermediaryBankSwift = dto.IntermediaryBankSwift,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await repo.CreateAsync(entity);
            return Results.Created($"/api/beneficiaries/{entity.Id}", new BeneficiaryDto
            {
                Id = entity.Id,
                Type = entity.Type,
                Name = entity.Name,
                AccountNumber = entity.AccountNumber,
                Address = entity.Address,
                Country = entity.Country,
                Bank = entity.Bank,
                Amount = entity.Amount,
                IntermediaryBankName = entity.IntermediaryBankName,
                IntermediaryBankSwift = entity.IntermediaryBankSwift,

            });
        }

        public static async Task<IResult> DeleteCompanyBeneficiary(
            int id,
            HttpContext ctx,
            [FromServices] IBeneficiaryRepository repo,
            [FromServices] IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            var b = await repo.GetByIdAsync(id);
            if (b == null || b.CompanyId != me.CompanyId.Value || b.IsDeleted)
                return Results.NotFound();

            b.IsDeleted = true;
            b.UpdatedAt = DateTime.UtcNow;
            await repo.UpdateAsync(b);

            return Results.NoContent();
        }
    }
}