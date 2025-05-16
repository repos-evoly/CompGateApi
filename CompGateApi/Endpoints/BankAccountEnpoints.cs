using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CompGateApi.Data.Models;
using FluentValidation;
using CompGateApi.Abstractions;

namespace CompGateApi.Endpoints
{
    public class BankAccountEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var ba = app
                .MapGroup("/api/bankaccounts")
                .RequireAuthorization("RequireCompanyUser");

            ba.MapGet("/", GetBankAccounts)
              .WithName("GetBankAccounts")
              .Produces<PagedResult<BankAccountDto>>(200);

            ba.MapGet("/{id:int}", GetBankAccountById)
              .WithName("GetBankAccountById")
              .Produces<BankAccountDto>(200)
              .Produces(404);

            ba.MapPost("/", CreateBankAccount)
              .WithName("CreateBankAccount")
              .Accepts<BankAccountCreateDto>("application/json")
              .Produces<BankAccountDto>(201)
              .Produces(400);

            ba.MapPut("/{id:int}", UpdateBankAccount)
              .WithName("UpdateBankAccount")
              .Accepts<BankAccountUpdateDto>("application/json")
              .Produces<BankAccountDto>(200)
              .Produces(400)
              .Produces(404);

            ba.MapDelete("/{id:int}", DeleteBankAccount)
              .WithName("DeleteBankAccount")
              .Produces(200)
              .Produces(404);
        }

        public static async Task<IResult> GetBankAccounts(
            [FromServices] IBankAccountRepository repo,
            [FromServices] IMapper mapper,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            var list = await repo.GetAllAsync(searchTerm, searchBy, page, limit);
            var dtos = mapper.Map<List<BankAccountDto>>(list);
            var total = await repo.GetCountAsync(searchTerm, searchBy);
            var totalPages = (int)Math.Ceiling((double)total / limit);

            return Results.Ok(new PagedResult<BankAccountDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalPages = totalPages,
                TotalRecords = total
            });
        }

        public static async Task<IResult> GetBankAccountById(
            int id,
            [FromServices] IBankAccountRepository repo,
            [FromServices] IMapper mapper)
        {
            var acct = await repo.GetByIdAsync(id);
            if (acct == null) return Results.NotFound("BankAccount not found.");
            return Results.Ok(mapper.Map<BankAccountDto>(acct));
        }

        public static async Task<IResult> CreateBankAccount(
            [FromBody] BankAccountCreateDto dto,
            [FromServices] IBankAccountRepository repo,
            [FromServices] IMapper mapper,
            [FromServices] IValidator<BankAccountCreateDto> validator)
        {
            var result = await validator.ValidateAsync(dto);
            if (!result.IsValid)
                return Results.BadRequest(result.Errors.Select(e => e.ErrorMessage));

            var entity = mapper.Map<BankAccount>(dto);
            await repo.CreateAsync(entity);
            var outDto = mapper.Map<BankAccountDto>(entity);
            return Results.Created($"/api/bankaccounts/{outDto.Id}", outDto);
        }

        public static async Task<IResult> UpdateBankAccount(
            int id,
            [FromBody] BankAccountUpdateDto dto,
            [FromServices] IBankAccountRepository repo,
            [FromServices] IMapper mapper,
            [FromServices] IValidator<BankAccountUpdateDto> validator)
        {
            var existing = await repo.GetByIdAsync(id);
            if (existing == null) return Results.NotFound("BankAccount not found.");

            var result = await validator.ValidateAsync(dto);
            if (!result.IsValid)
                return Results.BadRequest(result.Errors.Select(e => e.ErrorMessage));

            mapper.Map(dto, existing);
            await repo.UpdateAsync(existing);
            return Results.Ok(mapper.Map<BankAccountDto>(existing));
        }

        public static async Task<IResult> DeleteBankAccount(
            int id,
            [FromServices] IBankAccountRepository repo)
        {
            var existing = await repo.GetByIdAsync(id);
            if (existing == null) return Results.NotFound("BankAccount not found.");
            await repo.DeleteAsync(id);
            return Results.Ok("BankAccount deleted.");
        }
    }
}
