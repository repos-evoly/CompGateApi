using System.Security.Claims;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Beneficiaries;
using CompGateApi.Core.Dtos;
using CompGateApi.Core.LyPay;
using CompGateApi.Core.OnePay;
using CompGateApi.Data.Models;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Endpoints;

public sealed class BeneficiaryEndpoints : IEndpoints
{
    private const string MakerPermission = "canCreateTransfer";
    private const string CheckerPermission = "canposttransfer";

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
        if (paymentRail != PaymentRail.Normal && !HasRailReadPermission(currentUser))
            return Results.Forbid();

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
        if (beneficiary.PaymentRail != PaymentRail.Normal && !HasRailReadPermission(currentUser))
            return Results.Forbid();

        return Results.Ok(MapBeneficiary(beneficiary));
    }

    public static async Task<IResult> CreateCompanyBeneficiary(
        [FromBody] BeneficiaryCreateDto dto,
        HttpContext context,
        IBeneficiaryRepository repository,
        IUserRepository userRepository,
        OnePayTransferService onePayService,
        LyPayTransferService lyPayService)
    {
        var authUserId = GetAuthUserId(context);
        var currentUser = await GetCurrentUserAsync(context, userRepository);
        if (currentUser is null || !currentUser.CompanyId.HasValue)
            return Results.Unauthorized();

        var preparation = await PrepareInputAsync(
            authUserId,
            currentUser,
            dto.PaymentRail,
            dto.Type,
            dto.Name,
            dto.AccountNumber,
            dto.InstitutionId,
            dto.ProviderInstitutionReference,
            dto.Address,
            dto.Country,
            dto.Bank,
            dto.IntermediaryBankSwift,
            dto.IntermediaryBankName,
            dto.Language,
            onePayService,
            lyPayService,
            context.RequestAborted);
        if (preparation.Error is not null)
            return preparation.Error;

        var input = preparation.Value!;
        if (input.PaymentRail != PaymentRail.Normal &&
            await repository.ActiveDestinationExistsAsync(
                currentUser.CompanyId.Value,
                input.PaymentRail,
                input.InstitutionId!,
                input.AccountNumber))
        {
            return Results.Conflict(new { message = "This beneficiary destination already exists for the selected payment rail." });
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new Beneficiary
        {
            CompanyId = currentUser.CompanyId.Value,
            CreatedByUserId = currentUser.UserId,
            PaymentRail = input.PaymentRail,
            Type = input.Type,
            Name = input.Name,
            AccountNumber = input.AccountNumber,
            InstitutionId = input.InstitutionId,
            ProviderInstitutionReference = input.ProviderInstitutionReference,
            InstitutionName = input.InstitutionName,
            Address = TrimOrNull(dto.Address),
            Country = TrimOrNull(dto.Country),
            Bank = input.PaymentRail == PaymentRail.Normal
                ? TrimOrNull(dto.Bank)
                : Limit(input.InstitutionName!, 100),
            Amount = dto.Amount,
            IntermediaryBankName = TrimOrNull(dto.IntermediaryBankName),
            IntermediaryBankSwift = TrimOrNull(dto.IntermediaryBankSwift),
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        try
        {
            await repository.CreateAsync(entity);
        }
        catch (DbUpdateException exception) when (IsUniqueDestinationViolation(exception))
        {
            return Results.Conflict(new { message = "This beneficiary destination already exists for the selected payment rail." });
        }
        return Results.Created($"/api/beneficiaries/{entity.Id}?rail={entity.PaymentRail}", MapBeneficiary(entity));
    }

    public static async Task<IResult> UpdateCompanyBeneficiary(
        int id,
        [FromBody] BeneficiaryUpdateDto dto,
        HttpContext context,
        IBeneficiaryRepository repository,
        IUserRepository userRepository,
        OnePayTransferService onePayService,
        LyPayTransferService lyPayService)
    {
        var authUserId = GetAuthUserId(context);
        var currentUser = await GetCurrentUserAsync(context, userRepository);
        if (currentUser is null || !currentUser.CompanyId.HasValue)
            return Results.Unauthorized();

        var beneficiary = await repository.GetByIdAsync(id);
        if (beneficiary is null || beneficiary.CompanyId != currentUser.CompanyId.Value || beneficiary.IsDeleted)
            return Results.NotFound();

        if (dto.PaymentRail.HasValue && !Enum.IsDefined(dto.PaymentRail.Value))
            return Results.BadRequest(new { message = "Payment rail is invalid." });
        if (dto.PaymentRail.HasValue && dto.PaymentRail.Value != beneficiary.PaymentRail)
            return Results.BadRequest(new { message = "Payment rail cannot be changed after a beneficiary is created." });

        if (string.IsNullOrWhiteSpace(dto.RowVersion))
            return Results.BadRequest(new { message = "Row version is required." });

        byte[] expectedRowVersion;
        try
        {
            expectedRowVersion = Convert.FromBase64String(dto.RowVersion);
            if (expectedRowVersion.Length != 8)
                return Results.BadRequest(new { message = "Row version is invalid." });
        }
        catch (FormatException)
        {
            return Results.BadRequest(new { message = "Row version is invalid." });
        }

        var paymentRail = beneficiary.PaymentRail;
        var preparation = await PrepareInputAsync(
            authUserId,
            currentUser,
            paymentRail,
            dto.Type ?? beneficiary.Type,
            dto.Name,
            dto.AccountNumber,
            dto.InstitutionId,
            dto.ProviderInstitutionReference,
            dto.Address,
            dto.Country,
            dto.Bank,
            dto.IntermediaryBankSwift,
            dto.IntermediaryBankName,
            dto.Language,
            onePayService,
            lyPayService,
            context.RequestAborted,
            beneficiary.PaymentRail);
        if (preparation.Error is not null)
            return preparation.Error;

        var input = preparation.Value!;
        if (input.PaymentRail != PaymentRail.Normal &&
            await repository.ActiveDestinationExistsAsync(
                currentUser.CompanyId.Value,
                input.PaymentRail,
                input.InstitutionId!,
                input.AccountNumber,
                beneficiary.Id))
        {
            return Results.Conflict(new { message = "This beneficiary destination already exists for the selected payment rail." });
        }

        beneficiary.PaymentRail = input.PaymentRail;
        beneficiary.Type = input.Type;
        beneficiary.Name = input.Name;
        beneficiary.AccountNumber = input.AccountNumber;
        beneficiary.InstitutionId = input.InstitutionId;
        beneficiary.ProviderInstitutionReference = input.ProviderInstitutionReference;
        beneficiary.InstitutionName = input.InstitutionName;
        beneficiary.Address = TrimOrNull(dto.Address);
        beneficiary.Country = TrimOrNull(dto.Country);
        beneficiary.Bank = input.PaymentRail == PaymentRail.Normal
            ? TrimOrNull(dto.Bank)
            : Limit(input.InstitutionName!, 100);
        beneficiary.Amount = dto.Amount;
        beneficiary.IntermediaryBankName = TrimOrNull(dto.IntermediaryBankName);
        beneficiary.IntermediaryBankSwift = TrimOrNull(dto.IntermediaryBankSwift);
        beneficiary.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await repository.UpdateAsync(beneficiary, expectedRowVersion);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { message = "The beneficiary was changed by another user. Reload it and try again." });
        }
        catch (DbUpdateException exception) when (IsUniqueDestinationViolation(exception))
        {
            return Results.Conflict(new { message = "This beneficiary destination already exists for the selected payment rail." });
        }

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
        if (beneficiary.PaymentRail != PaymentRail.Normal && !HasMakerPermission(currentUser))
            return Results.Forbid();

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

    private static async Task<PreparedInputResult> PrepareInputAsync(
        int authUserId,
        UserDetailsDto currentUser,
        PaymentRail paymentRail,
        string type,
        string name,
        string accountNumber,
        string? institutionId,
        string? providerInstitutionReference,
        string? address,
        string? country,
        string? bank,
        string? intermediaryBankSwift,
        string? intermediaryBankName,
        string language,
        OnePayTransferService onePayService,
        LyPayTransferService lyPayService,
        CancellationToken cancellationToken,
        PaymentRail? existingPaymentRail = null)
    {
        if (!Enum.IsDefined(paymentRail))
            return PreparedInputResult.Fail(Results.BadRequest(new { message = "Payment rail is invalid." }));

        var normalizedType = paymentRail == PaymentRail.Normal
            ? type?.Trim() ?? string.Empty
            : "local";
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedAccount = PaymentBeneficiaryRules.NormalizeAccount(paymentRail, accountNumber);
        var normalizedInstitutionId = PaymentBeneficiaryRules.NormalizeInstitutionId(institutionId);

        var commonError = ValidateCommonInput(
            normalizedType,
            normalizedName,
            normalizedAccount,
            address,
            country,
            bank,
            intermediaryBankSwift,
            intermediaryBankName);
        if (commonError is not null)
            return PreparedInputResult.Fail(Results.BadRequest(new { message = commonError }));

        if (paymentRail == PaymentRail.Normal)
        {
            if (existingPaymentRail is not null &&
                existingPaymentRail != PaymentRail.Normal &&
                !HasMakerPermission(currentUser))
            {
                return PreparedInputResult.Fail(Results.Forbid());
            }

            return PreparedInputResult.Ok(new PreparedInput(
                paymentRail,
                normalizedType,
                normalizedName,
                normalizedAccount,
                null,
                null,
                null));
        }

        if (!HasMakerPermission(currentUser))
            return PreparedInputResult.Fail(Results.Forbid());

        var destinationError = PaymentBeneficiaryRules.ValidateDestination(
            paymentRail,
            normalizedInstitutionId,
            normalizedAccount);
        if (destinationError is not null)
            return PreparedInputResult.Fail(Results.BadRequest(new { message = destinationError }));

        ResolvedInstitutionResult resolved;
        if (paymentRail == PaymentRail.OnePay)
        {
            var providerUser = await onePayService.GetCurrentUserAsync(authUserId, cancellationToken);
            if (providerUser is null)
                return PreparedInputResult.Fail(Results.Unauthorized());

            var institutions = await onePayService.GetInstitutionsAsync(providerUser, language, cancellationToken);
            if (!institutions.Success)
                return PreparedInputResult.Fail(ToResult(institutions));

            var institution = institutions.Value!.FirstOrDefault(item =>
                string.Equals(item.InstitutionId, normalizedInstitutionId, StringComparison.OrdinalIgnoreCase));
            if (institution is null)
                return PreparedInputResult.Fail(Results.BadRequest(new { message = "The selected institution is not active for OnePay." }));

            resolved = ResolvedInstitutionResult.From(
                institution.InstitutionId,
                institution.Reference,
                InstitutionName(institution.FullName, institution.ShortName, institution.InstitutionId));
        }
        else
        {
            var providerUser = await lyPayService.GetCurrentUserAsync(authUserId, cancellationToken);
            if (providerUser is null)
                return PreparedInputResult.Fail(Results.Unauthorized());

            var institutions = await lyPayService.GetInstitutionsAsync(providerUser, language, cancellationToken);
            if (!institutions.Success)
                return PreparedInputResult.Fail(ToResult(institutions));

            var institution = institutions.Value!.FirstOrDefault(item =>
                string.Equals(item.InstitutionId, normalizedInstitutionId, StringComparison.Ordinal));
            if (institution is null)
                return PreparedInputResult.Fail(Results.BadRequest(new { message = "The selected institution is not active for LyPay." }));

            resolved = ResolvedInstitutionResult.From(
                institution.InstitutionId,
                institution.Reference,
                InstitutionName(institution.FullName, institution.ShortName, institution.InstitutionId));
        }

        if (resolved.InstitutionId.Length > 50 || resolved.ProviderReference?.Length > 50)
        {
            return PreparedInputResult.Fail(Results.Json(
                new { message = "The provider returned an institution reference that is too long." },
                statusCode: StatusCodes.Status503ServiceUnavailable));
        }

        var suppliedProviderReference = TrimOrNull(providerInstitutionReference);
        if (suppliedProviderReference is not null &&
            !string.Equals(suppliedProviderReference, resolved.ProviderReference, StringComparison.OrdinalIgnoreCase))
        {
            return PreparedInputResult.Fail(Results.BadRequest(new { message = "The provider institution reference does not match the selected institution." }));
        }

        return PreparedInputResult.Ok(new PreparedInput(
            paymentRail,
            normalizedType,
            normalizedName,
            normalizedAccount,
            resolved.InstitutionId,
            resolved.ProviderReference,
            Limit(resolved.InstitutionName, 200)));
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

    private static bool HasMakerPermission(UserDetailsDto user) =>
        user.Permissions.Any(permission =>
            string.Equals(permission, MakerPermission, StringComparison.OrdinalIgnoreCase));

    private static bool HasRailReadPermission(UserDetailsDto user) =>
        user.Permissions.Any(permission =>
            string.Equals(permission, MakerPermission, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(permission, CheckerPermission, StringComparison.OrdinalIgnoreCase));

    private static bool IsUniqueDestinationViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException &&
        sqlException.Number is 2601 or 2627;

    private static IResult ToResult<T>(OnePayServiceResult<T> result)
    {
        var payload = new { message = result.Error ?? "OnePay request failed." };
        return result.FailureKind switch
        {
            OnePayFailureKind.Forbidden => Results.Json(payload, statusCode: StatusCodes.Status403Forbidden),
            OnePayFailureKind.NotFound => Results.NotFound(payload),
            OnePayFailureKind.Conflict => Results.Conflict(payload),
            OnePayFailureKind.Unavailable => Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.BadRequest(payload)
        };
    }

    private static IResult ToResult<T>(LyPayServiceResult<T> result)
    {
        var payload = new { message = result.Error ?? "LyPay request failed." };
        return result.FailureKind switch
        {
            LyPayFailureKind.Forbidden => Results.Json(payload, statusCode: StatusCodes.Status403Forbidden),
            LyPayFailureKind.NotFound => Results.NotFound(payload),
            LyPayFailureKind.Conflict => Results.Conflict(payload),
            LyPayFailureKind.Unavailable => Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.BadRequest(payload)
        };
    }

    private static string InstitutionName(string fullName, string shortName, string fallback) =>
        !string.IsNullOrWhiteSpace(fullName)
            ? fullName.Trim()
            : !string.IsNullOrWhiteSpace(shortName)
                ? shortName.Trim()
                : fallback;

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Limit(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private sealed record PreparedInput(
        PaymentRail PaymentRail,
        string Type,
        string Name,
        string AccountNumber,
        string? InstitutionId,
        string? ProviderInstitutionReference,
        string? InstitutionName);

    private sealed record PreparedInputResult(PreparedInput? Value, IResult? Error)
    {
        public static PreparedInputResult Ok(PreparedInput value) => new(value, null);
        public static PreparedInputResult Fail(IResult error) => new(null, error);
    }

    private sealed record ResolvedInstitutionResult(
        string InstitutionId,
        string? ProviderReference,
        string InstitutionName)
    {
        public static ResolvedInstitutionResult From(
            string institutionId,
            string? providerReference,
            string institutionName) =>
            new(institutionId.Trim(), TrimOrNull(providerReference), institutionName.Trim());
    }
}
