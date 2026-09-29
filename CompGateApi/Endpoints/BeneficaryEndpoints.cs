using System.Security.Claims;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Endpoints;

public sealed class BeneficiaryEndpoints : IEndpoints
{
    public void RegisterEndpoints(WebApplication app)
    {
        var beneficiaries = app.MapGroup("/api/beneficiaries")
            .WithTags("Beneficiaries")
            .RequireAuthorization("RequireCompanyUser");

        beneficiaries.MapGet("/", GetCompanyBeneficiaries)
            .Produces<PagedResult<BeneficiaryDto>>(StatusCodes.Status200OK);

        beneficiaries.MapGet("/{id:int}", GetCompanyBeneficiaryById)
            .Produces<BeneficiaryDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        beneficiaries.MapPost("/", CreateCompanyBeneficiary)
            .Accepts<BeneficiaryCreateDto>("application/json")
            .Produces<BeneficiaryDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        beneficiaries.MapPut("/{id:int}", UpdateCompanyBeneficiary)
            .Accepts<BeneficiaryUpdateDto>("application/json")
            .Produces<BeneficiaryDto>(StatusCodes.Status200OK);

        // Existing frontend convention retained alongside the REST-style PUT route.
        beneficiaries.MapPost("/{id:int}/update", UpdateCompanyBeneficiary)
            .Accepts<BeneficiaryUpdateDto>("application/json")
            .Produces<BeneficiaryDto>(StatusCodes.Status200OK);

        beneficiaries.MapPost("/{id:int}/archive", ArchiveCompanyBeneficiary)
            .Produces(StatusCodes.Status204NoContent);

        // Legacy aliases remain soft-archive operations; no beneficiary is hard-deleted.
        beneficiaries.MapDelete("/{id:int}", ArchiveCompanyBeneficiary)
            .Produces(StatusCodes.Status204NoContent);
        beneficiaries.MapPost("/{id:int}/delete", ArchiveCompanyBeneficiary)
            .Produces(StatusCodes.Status204NoContent);
    }

    public static async Task<IResult> GetCompanyBeneficiaries(
        HttpContext context,
        IBeneficiaryRepository repository,
        IUserRepository userRepository,
        [FromQuery] string? rail = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? searchBy = null,
        [FromQuery] string? search = null)
    {
        var currentUser = await GetCurrentUserAsync(context, userRepository);
        if (currentUser is null || !currentUser.CompanyId.HasValue)
            return Results.Unauthorized();
        var paymentRail = PaymentRail.Normal;
        if (!string.IsNullOrWhiteSpace(rail) &&
            (!Enum.TryParse(rail, ignoreCase: true, out paymentRail) || !Enum.IsDefined(paymentRail)))
            return Results.BadRequest(new { message = "Payment rail is invalid." });
        if (paymentRail != PaymentRail.Normal)
            return Results.BadRequest(new { message = "OnePay and LyPay beneficiaries are disabled." });

        page = Math.Max(1, page);
        // The internal-transfer form already requests up to 1,000 normal beneficiaries.
        limit = Math.Clamp(limit, 1, 1000);
        var effectiveSearch = string.IsNullOrWhiteSpace(searchTerm) ? search : searchTerm;
        var (items, total) = await repository.GetPageByCompanyAsync(
            currentUser.CompanyId.Value,
            paymentRail,
            effectiveSearch,
            searchBy,
            page,
            limit);

        return Results.Ok(new PagedResult<BeneficiaryDto>
        {
            Data = items.Select(MapBeneficiary).ToList(),
            Page = page,
            Limit = limit,
            TotalRecords = total,
            TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)limit))
        });
    }

    public static async Task<IResult> GetCompanyBeneficiaryById(
        int id,
        HttpContext context,
        IBeneficiaryRepository repository,
        IUserRepository userRepository)
    {
        var currentUser = await GetCurrentUserAsync(context, userRepository);
        if (currentUser is null || !currentUser.CompanyId.HasValue)
            return Results.Unauthorized();

        var beneficiary = await repository.GetByIdAsync(id);
        if (beneficiary is null ||
            beneficiary.CompanyId != currentUser.CompanyId.Value ||
            beneficiary.IsDeleted)
        {
            return Results.NotFound();
        }

        return Results.Ok(MapBeneficiary(beneficiary));
    }

    public static async Task<IResult> CreateCompanyBeneficiary(
        [FromBody] BeneficiaryCreateDto dto,
        HttpContext context,
        IBeneficiaryRepository repository,
        IUserRepository userRepository)
    {
        var currentUser = await GetCurrentUserAsync(context, userRepository);
        if (currentUser is null || !currentUser.CompanyId.HasValue)
            return Results.Unauthorized();
        if (dto.PaymentRail != PaymentRail.Normal)
            return Results.BadRequest(new { message = "OnePay and LyPay beneficiaries are disabled." });

        var error = ValidateCommonInput(dto.Type?.Trim() ?? "", dto.Name?.Trim() ?? "",
            dto.AccountNumber?.Trim() ?? "", dto.Address, dto.Country, dto.Bank,
            dto.IntermediaryBankSwift, dto.IntermediaryBankName);
        if (error is not null) return Results.BadRequest(new { message = error });

        var now = DateTimeOffset.UtcNow;
        var entity = new Beneficiary
        {
            CompanyId = currentUser.CompanyId.Value,
            Type = dto.Type!.Trim(),
            Name = dto.Name!.Trim(),
            AccountNumber = dto.AccountNumber!.Trim(),
            Address = TrimOrNull(dto.Address),
            Country = TrimOrNull(dto.Country),
            Bank = TrimOrNull(dto.Bank),
            Amount = dto.Amount,
            IntermediaryBankName = TrimOrNull(dto.IntermediaryBankName),
            IntermediaryBankSwift = TrimOrNull(dto.IntermediaryBankSwift),
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };
        await repository.CreateAsync(entity);
        return Results.Created($"/api/beneficiaries/{entity.Id}", MapBeneficiary(entity));
    }

    public static async Task<IResult> UpdateCompanyBeneficiary(
        int id,
        [FromBody] BeneficiaryUpdateDto dto,
        HttpContext context,
        IBeneficiaryRepository repository,
        IUserRepository userRepository)
    {
        var currentUser = await GetCurrentUserAsync(context, userRepository);
        if (currentUser is null || !currentUser.CompanyId.HasValue)
            return Results.Unauthorized();

        var beneficiary = await repository.GetByIdAsync(id);
        if (beneficiary is null || beneficiary.CompanyId != currentUser.CompanyId.Value || beneficiary.IsDeleted)
            return Results.NotFound();
        if (dto.PaymentRail.HasValue && dto.PaymentRail != PaymentRail.Normal)
            return Results.BadRequest(new { message = "OnePay and LyPay beneficiaries are disabled." });

        var type = (dto.Type ?? beneficiary.Type).Trim();
        var error = ValidateCommonInput(type, dto.Name?.Trim() ?? "",
            dto.AccountNumber?.Trim() ?? "", dto.Address, dto.Country, dto.Bank,
            dto.IntermediaryBankSwift, dto.IntermediaryBankName);
        if (error is not null) return Results.BadRequest(new { message = error });

        // Restore ordinary editing without requiring the unapplied rail RowVersion column.
        beneficiary.Type = type;
        beneficiary.Name = dto.Name!.Trim();
        beneficiary.AccountNumber = dto.AccountNumber!.Trim();
        beneficiary.Address = TrimOrNull(dto.Address);
        beneficiary.Country = TrimOrNull(dto.Country);
        beneficiary.Bank = TrimOrNull(dto.Bank);
        beneficiary.Amount = dto.Amount;
        beneficiary.IntermediaryBankName = TrimOrNull(dto.IntermediaryBankName);
        beneficiary.IntermediaryBankSwift = TrimOrNull(dto.IntermediaryBankSwift);
        beneficiary.UpdatedAt = DateTimeOffset.UtcNow;
        await repository.UpdateAsync(beneficiary);
        return Results.Ok(MapBeneficiary(beneficiary));
    }

    public static async Task<IResult> ArchiveCompanyBeneficiary(
        int id,
        HttpContext context,
        IBeneficiaryRepository repository,
        IUserRepository userRepository)
    {
        var currentUser = await GetCurrentUserAsync(context, userRepository);
        if (currentUser is null || !currentUser.CompanyId.HasValue)
            return Results.Unauthorized();

        var beneficiary = await repository.GetByIdAsync(id);
        if (beneficiary is null || beneficiary.CompanyId != currentUser.CompanyId.Value || beneficiary.IsDeleted)
            return Results.NotFound();

        beneficiary.IsDeleted = true;
        beneficiary.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            await repository.UpdateAsync(beneficiary);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { message = "The beneficiary was changed by another user. Reload it and try again." });
        }

        return Results.NoContent();
    }

    private static string? ValidateCommonInput(
        string type,
        string name,
        string accountNumber,
        string? address,
        string? country,
        string? bank,
        string? intermediaryBankSwift,
        string? intermediaryBankName)
    {
        if (string.IsNullOrWhiteSpace(type)) return "Beneficiary type is required.";
        if (type.Length > 20) return "Beneficiary type cannot exceed 20 characters.";
        if (string.IsNullOrWhiteSpace(name)) return "Beneficiary name is required.";
        if (name.Length > 100) return "Beneficiary name cannot exceed 100 characters.";
        if (string.IsNullOrWhiteSpace(accountNumber)) return "Account number is required.";
        if (accountNumber.Length > 34) return "Account number cannot exceed 34 characters.";
        if (TrimOrNull(address)?.Length > 200) return "Address cannot exceed 200 characters.";
        if (TrimOrNull(country)?.Length > 100) return "Country cannot exceed 100 characters.";
        if (TrimOrNull(bank)?.Length > 100) return "Bank cannot exceed 100 characters.";
        if (TrimOrNull(intermediaryBankSwift)?.Length > 20) return "Intermediary bank SWIFT cannot exceed 20 characters.";
        if (TrimOrNull(intermediaryBankName)?.Length > 100) return "Intermediary bank name cannot exceed 100 characters.";
        return null;
    }

    private static BeneficiaryDto MapBeneficiary(Beneficiary beneficiary) => new()
    {
        Id = beneficiary.Id,
        PaymentRail = beneficiary.PaymentRail,
        Type = beneficiary.Type,
        Name = beneficiary.Name,
        AccountNumber = beneficiary.AccountNumber,
        InstitutionId = beneficiary.InstitutionId,
        ProviderInstitutionReference = beneficiary.ProviderInstitutionReference,
        InstitutionName = beneficiary.InstitutionName,
        CreatedByUserId = beneficiary.CreatedByUserId,
        Address = beneficiary.Address,
        Country = beneficiary.Country,
        Bank = beneficiary.Bank,
        Amount = beneficiary.Amount,
        IntermediaryBankName = beneficiary.IntermediaryBankName,
        IntermediaryBankSwift = beneficiary.IntermediaryBankSwift,
        CreatedAt = beneficiary.CreatedAt,
        UpdatedAt = beneficiary.UpdatedAt,
        RowVersion = beneficiary.RowVersion.Length == 0
            ? string.Empty
            : Convert.ToBase64String(beneficiary.RowVersion)
    };

    private static async Task<UserDetailsDto?> GetCurrentUserAsync(
        HttpContext context,
        IUserRepository userRepository)
    {
        var bearer = context.Request.Headers.Authorization.FirstOrDefault() ?? string.Empty;
        return await userRepository.GetUserByAuthId(GetAuthUserId(context), bearer);
    }

    private static int GetAuthUserId(HttpContext context)
    {
        var raw = context.User.FindFirst("nameid")?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(raw, out var id)) return id;
        throw new UnauthorizedAccessException("Missing/invalid 'nameid' claim.");
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
