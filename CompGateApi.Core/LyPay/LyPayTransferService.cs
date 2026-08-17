using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Core.OnePay;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompGateApi.Core.LyPay;

public enum LyPayFailureKind
{
    BadRequest,
    Forbidden,
    NotFound,
    Conflict,
    Unavailable
}

public sealed record LyPayServiceResult<T>(
    bool Success,
    T? Value,
    string? Error,
    LyPayFailureKind? FailureKind)
{
    public static LyPayServiceResult<T> Ok(T value) => new(true, value, null, null);

    public static LyPayServiceResult<T> Fail(string error, LyPayFailureKind kind) =>
        new(false, default, error, kind);
}

public sealed class LyPayTransferService
{
    private const string MakerPermission = "canCreateTransfer";
    private const string CheckerPermission = "canposttransfer";

    private readonly CompGateApiDbContext _db;
    private readonly ITransferRequestRepository _transferRepository;
    private readonly ILyPayProviderClient _provider;
    private readonly IMemoryCache _cache;
    private readonly OnePayOptions _options;
    private readonly LyPayReconciliationOptions _reconciliation;
    private readonly ILogger<LyPayTransferService> _logger;

    public LyPayTransferService(
        CompGateApiDbContext db,
        ITransferRequestRepository transferRepository,
        ILyPayProviderClient provider,
        IMemoryCache cache,
        IOptions<OnePayOptions> options,
        IOptions<LyPayReconciliationOptions> reconciliation,
        ILogger<LyPayTransferService> logger)
    {
        _db = db;
        _transferRepository = transferRepository;
        _provider = provider;
        _cache = cache;
        _options = options.Value;
        _reconciliation = reconciliation.Value;
        _logger = logger;
    }

    public bool IsProviderConfigured => _provider.IsConfigured;

    public bool CanCreate(LyPayCurrentUser user) => user.HasPermission(MakerPermission);

    public bool CanApprove(LyPayCurrentUser user) => user.HasPermission(CheckerPermission);

    public bool CanView(LyPayCurrentUser user) => CanCreate(user) || CanApprove(user);

    public async Task<LyPayCurrentUser?> GetCurrentUserAsync(
        int authUserId,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(x =>
                x.AuthUserId == authUserId &&
                x.IsActive &&
                x.CompanyId.HasValue &&
                x.Company != null &&
                x.Company.IsActive)
            .Select(x => new
            {
                x.Id,
                CompanyId = x.CompanyId!.Value,
                CompanyCode = x.Company!.Code,
                CompanyPhone = x.Company.KycMobile,
                UserPhone = x.Phone,
                x.FirstName,
                x.LastName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return null;

        var permissions = await _db.UserRolePermissions
            .AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .Select(x => x.Permission.NameEn)
            .Where(x => x != null && x != string.Empty)
            .ToListAsync(cancellationToken);

        return new LyPayCurrentUser
        {
            UserId = user.Id,
            CompanyId = user.CompanyId,
            CompanyCode = user.CompanyCode.Trim(),
            CompanyPhone = user.CompanyPhone?.Trim() ?? string.Empty,
            UserPhone = user.UserPhone?.Trim() ?? string.Empty,
            DisplayName = $"{user.FirstName} {user.LastName}".Trim(),
            Permissions = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase)
        };
    }

    public async Task<LyPayServiceResult<IReadOnlyList<LyPayAccountDto>>> GetAccountsAsync(
        LyPayCurrentUser user,
        CancellationToken cancellationToken)
    {
        if (!CanCreate(user))
            return LyPayServiceResult<IReadOnlyList<LyPayAccountDto>>.Fail("Permission denied.", LyPayFailureKind.Forbidden);

        try
        {
            var accounts = await _transferRepository.GetAccountsAsync(user.CompanyCode);
            var result = accounts
                .Where(x => !string.IsNullOrWhiteSpace(x.AccountString))
                .Select(x => new LyPayAccountDto
                {
                    AccountNumber = x.AccountString.Trim(),
                    AccountName = x.AccountName?.Trim() ?? string.Empty,
                    Currency = x.Currency?.Trim().ToUpperInvariant() ?? string.Empty,
                    AvailableBalance = x.AvailableBalance
                })
                .OrderBy(x => x.AccountNumber)
                .ToList();

            return LyPayServiceResult<IReadOnlyList<LyPayAccountDto>>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load LyPay source accounts for company {CompanyId}", user.CompanyId);
            return LyPayServiceResult<IReadOnlyList<LyPayAccountDto>>.Fail(
                "The company accounts could not be loaded.",
                LyPayFailureKind.Unavailable);
        }
    }

    public async Task<LyPayServiceResult<IReadOnlyList<LyPayInstitutionDto>>> GetInstitutionsAsync(
        LyPayCurrentUser user,
        string language,
        CancellationToken cancellationToken)
    {
        if (!CanCreate(user))
            return LyPayServiceResult<IReadOnlyList<LyPayInstitutionDto>>.Fail("Permission denied.", LyPayFailureKind.Forbidden);

        if (!_provider.IsConfigured)
        {
            return LyPayServiceResult<IReadOnlyList<LyPayInstitutionDto>>.Fail(
                "LyPay integration is not configured.",
                LyPayFailureKind.Unavailable);
        }

        var normalizedLanguage = NormalizeLanguage(language);
        var cacheKey = $"lypay:institutions:{user.CompanyCode}:{normalizedLanguage}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<LyPayInstitutionDto>? cached) && cached is not null)
            return LyPayServiceResult<IReadOnlyList<LyPayInstitutionDto>>.Ok(cached);

        var call = await _provider.GetInstitutionsAsync(
            user.CompanyCode,
            normalizedLanguage,
            CreateReference("LYI"),
            cancellationToken);

        if (!call.HttpSuccess || call.Main?.Success != true || call.Data is null)
        {
            return LyPayServiceResult<IReadOnlyList<LyPayInstitutionDto>>.Fail(
                ProviderError(call, "LyPay institutions could not be loaded."),
                LyPayFailureKind.Unavailable);
        }

        var institutions = (call.Data.Institutions ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.LYPayReference))
            .Select(x => new LyPayInstitutionDto
            {
                InstitutionId = x.LYPayReference!.Trim(),
                Reference = x.Reference?.Trim() ?? string.Empty,
                ShortName = x.ShortName?.Trim() ?? string.Empty,
                FullName = x.FullName?.Trim() ?? string.Empty
            })
            .GroupBy(x => x.InstitutionId, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => string.IsNullOrWhiteSpace(x.ShortName) ? x.FullName : x.ShortName)
            .ToList();

        _cache.Set(
            cacheKey,
            (IReadOnlyList<LyPayInstitutionDto>)institutions,
            TimeSpan.FromMinutes(Math.Max(1, _options.InstitutionCacheMinutes)));

        return LyPayServiceResult<IReadOnlyList<LyPayInstitutionDto>>.Ok(institutions);
    }

    public async Task<LyPayServiceResult<LyPayValidationDto>> ValidateAsync(
        LyPayCurrentUser user,
        LyPayValidateTransferRequestDto request,
        string? trustedDeviceId,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        if (!CanCreate(user))
            return LyPayServiceResult<LyPayValidationDto>.Fail("Permission denied.", LyPayFailureKind.Forbidden);

        if (!_provider.IsConfigured)
            return LyPayServiceResult<LyPayValidationDto>.Fail("LyPay integration is not configured.", LyPayFailureKind.Unavailable);

        if (request.BeneficiaryId.HasValue)
        {
            if (request.BeneficiaryId.Value <= 0)
                return LyPayServiceResult<LyPayValidationDto>.Fail("Beneficiary id is invalid.", LyPayFailureKind.BadRequest);

            var beneficiary = await _db.Beneficiaries
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.BeneficiaryId.Value &&
                    x.CompanyId == user.CompanyId &&
                    x.PaymentRail == PaymentRail.LyPay &&
                    !x.IsDeleted,
                    cancellationToken);
            if (beneficiary is null)
                return LyPayServiceResult<LyPayValidationDto>.Fail("LyPay beneficiary was not found.", LyPayFailureKind.NotFound);
            if (string.IsNullOrWhiteSpace(beneficiary.InstitutionId) || string.IsNullOrWhiteSpace(beneficiary.AccountNumber))
                return LyPayServiceResult<LyPayValidationDto>.Fail("The saved LyPay beneficiary destination is incomplete.", LyPayFailureKind.BadRequest);

            var savedInstitutionId = beneficiary.InstitutionId.Trim();
            var savedAccount = NormalizeDestinationAccount(beneficiary.AccountNumber);
            if (!string.IsNullOrWhiteSpace(request.ToInstitutionId) &&
                !string.Equals(request.ToInstitutionId.Trim(), savedInstitutionId, StringComparison.Ordinal))
            {
                return LyPayServiceResult<LyPayValidationDto>.Fail(
                    "The selected bank does not match the saved beneficiary.",
                    LyPayFailureKind.BadRequest);
            }
            if (!string.IsNullOrWhiteSpace(request.ToAccount) &&
                !string.Equals(NormalizeDestinationAccount(request.ToAccount), savedAccount, StringComparison.Ordinal))
            {
                return LyPayServiceResult<LyPayValidationDto>.Fail(
                    "The destination account does not match the saved beneficiary.",
                    LyPayFailureKind.BadRequest);
            }

            request.ToInstitutionId = savedInstitutionId;
            request.ToAccount = savedAccount;
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return LyPayServiceResult<LyPayValidationDto>.Fail(validationError, LyPayFailureKind.BadRequest);

        var fromAccount = request.FromAccount.Trim();
        var toAccount = NormalizeDestinationAccount(request.ToAccount);
        var accountNoResult = DeriveProviderAccountNo(fromAccount, user.CompanyCode);
        if (!accountNoResult.Success)
            return LyPayServiceResult<LyPayValidationDto>.Fail(accountNoResult.Error!, LyPayFailureKind.BadRequest);

        LyPayAccountDto? sourceAccount;
        var accountsResult = await GetAccountsAsync(user, cancellationToken);
        if (!accountsResult.Success)
            return LyPayServiceResult<LyPayValidationDto>.Fail(accountsResult.Error!, accountsResult.FailureKind!.Value);

        sourceAccount = accountsResult.Value!
            .FirstOrDefault(x => string.Equals(x.AccountNumber, fromAccount, StringComparison.Ordinal));
        if (sourceAccount is null)
            return LyPayServiceResult<LyPayValidationDto>.Fail("The selected source account does not belong to this company.", LyPayFailureKind.BadRequest);
        if (string.IsNullOrWhiteSpace(sourceAccount.AccountName))
            return LyPayServiceResult<LyPayValidationDto>.Fail("The source account name could not be resolved.", LyPayFailureKind.Unavailable);
        if (string.IsNullOrWhiteSpace(sourceAccount.Currency))
            return LyPayServiceResult<LyPayValidationDto>.Fail("The source account currency could not be resolved.", LyPayFailureKind.Unavailable);

        var institutionsResult = await GetInstitutionsAsync(user, request.Language, cancellationToken);
        if (!institutionsResult.Success)
            return LyPayServiceResult<LyPayValidationDto>.Fail(institutionsResult.Error!, institutionsResult.FailureKind!.Value);

        var institution = institutionsResult.Value!
            .FirstOrDefault(x => string.Equals(x.InstitutionId, request.ToInstitutionId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (institution is null)
            return LyPayServiceResult<LyPayValidationDto>.Fail("The selected bank is not active for LyPay.", LyPayFailureKind.BadRequest);
        if (!string.Equals(
                toAccount.Substring(4, 3),
                institution.InstitutionId,
                StringComparison.Ordinal))
        {
            return LyPayServiceResult<LyPayValidationDto>.Fail(
                "The bank code in the destination IBAN does not match the selected bank.",
                LyPayFailureKind.BadRequest);
        }

        var phone = !string.IsNullOrWhiteSpace(user.CompanyPhone)
            ? user.CompanyPhone
            : user.UserPhone;
        if (string.IsNullOrWhiteSpace(phone))
            return LyPayServiceResult<LyPayValidationDto>.Fail("A company or user phone number is required for LyPay.", LyPayFailureKind.BadRequest);

        var language = NormalizeLanguage(request.Language);
        var referenceNo = CreateReference("LYV");
        var providerCall = await _provider.ValidateOutgoingTransferAsync(
            new LyPayProviderValidationCommand(
                accountNoResult.Value!,
                language,
                referenceNo,
                fromAccount,
                sourceAccount.AccountName,
                phone,
                institution.InstitutionId,
                toAccount,
                request.Amount,
                sourceAccount.Currency,
                request.Description.Trim(),
                BuildDeviceInfo(trustedDeviceId, userAgent, request.DeviceInfo)),
            cancellationToken);

        var providerData = providerCall.Data;
        if (!providerCall.HttpSuccess || providerCall.Main?.Success != true || providerData is null)
        {
            return LyPayServiceResult<LyPayValidationDto>.Fail(
                ProviderError(providerCall, "LyPay rejected the transfer validation."),
                LyPayFailureKind.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(providerData.HostReference) ||
            string.IsNullOrWhiteSpace(providerData.DHBReference) ||
            string.IsNullOrWhiteSpace(providerData.AgreementReference) ||
            string.IsNullOrWhiteSpace(providerData.ToAccountName))
        {
            return LyPayServiceResult<LyPayValidationDto>.Fail(
                "LyPay returned an incomplete validation response.",
                LyPayFailureKind.Unavailable);
        }

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(Math.Max(1, _options.ValidationSessionMinutes));
        var session = new LyPayValidationSession
        {
            Id = Guid.NewGuid(),
            ReferenceNo = referenceNo,
            CompanyId = user.CompanyId,
            CreatedByUserId = user.UserId,
            CreatedByName = user.DisplayName,
            FromAccount = fromAccount,
            FromAccountName = sourceAccount.AccountName,
            FromAccountPhoneNo = phone,
            ToInstitutionId = institution.InstitutionId,
            ToInstitutionName = InstitutionDisplayName(institution),
            ToAccount = toAccount,
            ToAccountName = providerData.ToAccountName.Trim(),
            Amount = request.Amount,
            Currency = sourceAccount.Currency,
            Description = request.Description.Trim(),
            Language = language,
            HostReference = providerData.HostReference.Trim(),
            DhbReference = providerData.DHBReference.Trim(),
            AgreementReference = providerData.AgreementReference.Trim(),
            BankReferenceNo = providerCall.Main.BankReferenceNo?.Trim(),
            ValidatedAt = now,
            ExpiresAt = expiresAt
        };

        await PurgeValidationSessionsAsync(now, cancellationToken);
        _db.LyPayValidationSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        return LyPayServiceResult<LyPayValidationDto>.Ok(new LyPayValidationDto
        {
            ValidationToken = session.Id,
            ExpiresAt = session.ExpiresAt,
            ToAccountName = session.ToAccountName,
            InstitutionName = session.ToInstitutionName,
            FromAccount = session.FromAccount,
            ToAccount = session.ToAccount,
            Amount = session.Amount,
            Currency = session.Currency,
            Description = session.Description
        });
    }

    public async Task<LyPayServiceResult<LyPayTransferDto>> CreateAsync(
        LyPayCurrentUser user,
        Guid validationToken,
        CancellationToken cancellationToken)
    {
        if (!CanCreate(user))
            return LyPayServiceResult<LyPayTransferDto>.Fail("Permission denied.", LyPayFailureKind.Forbidden);
        if (validationToken == Guid.Empty)
            return LyPayServiceResult<LyPayTransferDto>.Fail("Validation token is required.", LyPayFailureKind.BadRequest);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var session = await _db.LyPayValidationSessions
                .FirstOrDefaultAsync(x =>
                    x.Id == validationToken &&
                    x.CompanyId == user.CompanyId &&
                    x.CreatedByUserId == user.UserId,
                    cancellationToken);

            if (session is null)
                return LyPayServiceResult<LyPayTransferDto>.Fail("Validation session was not found.", LyPayFailureKind.NotFound);
            if (session.ConsumedAt.HasValue)
                return LyPayServiceResult<LyPayTransferDto>.Fail("Validation session has already been used.", LyPayFailureKind.Conflict);
            if (session.ExpiresAt <= DateTimeOffset.UtcNow)
                return LyPayServiceResult<LyPayTransferDto>.Fail("Validation session has expired. Validate the transfer again.", LyPayFailureKind.Conflict);

            var transfer = new LyPayTransfer
            {
                ReferenceNo = session.ReferenceNo,
                CompanyId = session.CompanyId,
                CreatedByUserId = session.CreatedByUserId,
                CreatedByName = session.CreatedByName,
                FromAccount = session.FromAccount,
                FromAccountName = session.FromAccountName,
                FromAccountPhoneNo = session.FromAccountPhoneNo,
                ToInstitutionId = session.ToInstitutionId,
                ToInstitutionName = session.ToInstitutionName,
                ToAccount = session.ToAccount,
                ToAccountName = session.ToAccountName,
                Amount = session.Amount,
                Currency = session.Currency,
                Description = session.Description,
                Language = session.Language,
                Status = LyPayTransferStatus.PendingApproval,
                HostReference = session.HostReference,
                DhbReference = session.DhbReference,
                AgreementReference = session.AgreementReference,
                BankReferenceNo = session.BankReferenceNo,
                ValidatedAt = session.ValidatedAt
            };

            session.ConsumedAt = DateTimeOffset.UtcNow;
            _db.LyPayTransfers.Add(transfer);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return LyPayServiceResult<LyPayTransferDto>.Ok(MapTransfer(transfer, user));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return LyPayServiceResult<LyPayTransferDto>.Fail("Validation session has already been used.", LyPayFailureKind.Conflict);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _logger.LogWarning(ex, "Concurrent or invalid LyPay validation-session consumption was blocked");
            return LyPayServiceResult<LyPayTransferDto>.Fail("Validation session could not be consumed.", LyPayFailureKind.Conflict);
        }
    }

    public async Task<PagedResult<LyPayTransferDto>> GetTransfersAsync(
        LyPayCurrentUser user,
        int page,
        int limit,
        string? searchTerm,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);
        var query = _db.LyPayTransfers
            .AsNoTracking()
            .Where(x => x.CompanyId == user.CompanyId);

        var search = searchTerm?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var isStatusSearch = Enum.TryParse<LyPayTransferStatus>(
                search.Replace(" ", string.Empty),
                true,
                out var status);

            query = query.Where(x =>
                x.ReferenceNo.Contains(search) ||
                x.FromAccount.Contains(search) ||
                x.ToAccount.Contains(search) ||
                x.ToAccountName.Contains(search) ||
                x.ToInstitutionName.Contains(search) ||
                (isStatusSearch && x.Status == status));
        }

        var total = await query.CountAsync(cancellationToken);
        var transfers = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var data = transfers.Select(transfer => MapTransfer(transfer, user)).ToList();

        return new PagedResult<LyPayTransferDto>
        {
            Data = data,
            Page = page,
            Limit = limit,
            TotalRecords = total,
            TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)limit))
        };
    }

    public async Task<LyPayTransferDto?> GetTransferAsync(
        LyPayCurrentUser user,
        int id,
        CancellationToken cancellationToken)
    {
        var transfer = await _db.LyPayTransfers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id && x.CompanyId == user.CompanyId,
                cancellationToken);
        return transfer is null ? null : MapTransfer(transfer, user);
    }

    public async Task<LyPayServiceResult<LyPayTransferDto>> ApproveAndExecuteAsync(
        LyPayCurrentUser user,
        int id,
        CancellationToken cancellationToken)
    {
        if (!CanApprove(user))
            return LyPayServiceResult<LyPayTransferDto>.Fail("Permission denied.", LyPayFailureKind.Forbidden);
        if (!_provider.IsConfigured)
            return LyPayServiceResult<LyPayTransferDto>.Fail("LyPay integration is not configured.", LyPayFailureKind.Unavailable);

        var existing = await _db.LyPayTransfers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == user.CompanyId, cancellationToken);
        if (existing is null)
            return LyPayServiceResult<LyPayTransferDto>.Fail("LyPay transfer was not found.", LyPayFailureKind.NotFound);
        if (existing.Status != LyPayTransferStatus.PendingApproval)
            return LyPayServiceResult<LyPayTransferDto>.Fail("Only pending approval transfers can be executed.", LyPayFailureKind.Conflict);

        var accountNoResult = DeriveProviderAccountNo(existing.FromAccount, user.CompanyCode);
        if (!accountNoResult.Success)
            return LyPayServiceResult<LyPayTransferDto>.Fail(accountNoResult.Error!, LyPayFailureKind.BadRequest);

        var now = DateTimeOffset.UtcNow;
        var claimed = await _db.LyPayTransfers
            .Where(x =>
                x.Id == id &&
                x.CompanyId == user.CompanyId &&
                x.Status == LyPayTransferStatus.PendingApproval &&
                x.ExecutionClaimedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, LyPayTransferStatus.PendingProviderResponse)
                .SetProperty(x => x.ExecutionClaimedAt, now)
                .SetProperty(x => x.ApprovedByUserId, user.UserId)
                .SetProperty(x => x.ApprovedByName, user.DisplayName)
                .SetProperty(x => x.ApprovedAt, now)
                .SetProperty(x => x.UpdatedAt, now)
                // If this process stops during execute, reconciliation becomes
                // eligible only after the HTTP call's maximum lifetime plus a buffer.
                .SetProperty(x => x.NextStatusCheckAt, now.AddSeconds(ExecutionRecoveryDelaySeconds())),
                cancellationToken);

        if (claimed != 1)
            return LyPayServiceResult<LyPayTransferDto>.Fail("This transfer has already been claimed for execution.", LyPayFailureKind.Conflict);

        var transfer = await _db.LyPayTransfers.FirstAsync(x => x.Id == id, CancellationToken.None);
        var call = await _provider.ExecuteOutgoingTransferAsync(
            new LyPayProviderExecutionCommand(
                accountNoResult.Value!,
                transfer.Language,
                transfer.ReferenceNo,
                transfer.HostReference,
                transfer.DhbReference,
                transfer.AgreementReference,
                transfer.FromAccount,
                transfer.Amount,
                transfer.Currency,
                transfer.Description),
            cancellationToken);

        transfer.ExecutedAt = DateTimeOffset.UtcNow;
        ApplyProviderMain(transfer, call.Main);
        if (call.HttpSuccess &&
            call.Main?.Success == true &&
            call.Main.StillUnderProcessing == false)
        {
            transfer.Status = LyPayTransferStatus.Completed;
            transfer.CompletedAt = DateTimeOffset.UtcNow;
            transfer.NextStatusCheckAt = null;
            transfer.ProviderTransactionStatus ??= "completed";
        }
        else if (call.HttpSuccess && call.Main?.Success == true)
        {
            // A missing processing flag is financially ambiguous. Only an
            // explicit false response may be treated as immediate completion.
            transfer.Status = LyPayTransferStatus.PendingProviderResponse;
            transfer.NextStatusCheckAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, _reconciliation.RetryDelaySeconds));
        }
        else if (call.HttpSuccess && call.Main?.Success == false)
        {
            transfer.Status = LyPayTransferStatus.Failed;
            transfer.NextStatusCheckAt = null;
        }
        else
        {
            // A transport error or unreadable response is financially ambiguous.
            // Keep the one-time execution claim and reconcile only through status inquiry.
            transfer.Status = LyPayTransferStatus.PendingProviderResponse;
            transfer.ProviderReturnMessage = call.Error;
            transfer.NextStatusCheckAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, _reconciliation.RetryDelaySeconds));
        }

        await _db.SaveChangesAsync(CancellationToken.None);
        return LyPayServiceResult<LyPayTransferDto>.Ok(MapTransfer(transfer, user));
    }

    public async Task<LyPayServiceResult<LyPayTransferDto>> RefreshStatusAsync(
        LyPayCurrentUser user,
        int id,
        CancellationToken cancellationToken)
    {
        if (!CanView(user))
            return LyPayServiceResult<LyPayTransferDto>.Fail("Permission denied.", LyPayFailureKind.Forbidden);

        var transfer = await _db.LyPayTransfers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == user.CompanyId, cancellationToken);
        if (transfer is null)
            return LyPayServiceResult<LyPayTransferDto>.Fail("LyPay transfer was not found.", LyPayFailureKind.NotFound);
        if (transfer.Status is not LyPayTransferStatus.PendingProviderResponse and not LyPayTransferStatus.Unknown)
            return LyPayServiceResult<LyPayTransferDto>.Fail("Only pending or unknown transfers can be refreshed.", LyPayFailureKind.Conflict);
        if (transfer.Status == LyPayTransferStatus.PendingProviderResponse &&
            transfer.ExecutedAt is null &&
            (transfer.NextStatusCheckAt is null || transfer.NextStatusCheckAt > DateTimeOffset.UtcNow))
        {
            return LyPayServiceResult<LyPayTransferDto>.Fail(
                "The transfer execution is still in progress.",
                LyPayFailureKind.Conflict);
        }

        return await RefreshStatusInternalAsync(id, user.CompanyId, true, cancellationToken, user);
    }

    public async Task ProcessPendingTransfersAsync(CancellationToken cancellationToken)
    {
        if (!_reconciliation.Enabled || !_provider.IsConfigured)
            return;

        var now = DateTimeOffset.UtcNow;
        var maxStatusChecks = Math.Max(1, _reconciliation.MaxStatusChecks);
        var ids = await _db.LyPayTransfers
            .AsNoTracking()
            .Where(x =>
                x.Status == LyPayTransferStatus.PendingProviderResponse &&
                x.StatusCheckAttempts < maxStatusChecks &&
                x.NextStatusCheckAt != null &&
                x.NextStatusCheckAt <= now)
            .OrderBy(x => x.NextStatusCheckAt)
            .Select(x => new { x.Id, x.CompanyId })
            .Take(Math.Clamp(_reconciliation.BatchSize, 1, 100))
            .ToListAsync(cancellationToken);

        foreach (var item in ids)
        {
            try
            {
                await RefreshStatusInternalAsync(item.Id, item.CompanyId, false, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LyPay reconciliation failed for transfer {TransferId}", item.Id);
            }
        }
    }

    private async Task<LyPayServiceResult<LyPayTransferDto>> RefreshStatusInternalAsync(
        int id,
        int companyId,
        bool manual,
        CancellationToken cancellationToken,
        LyPayCurrentUser? viewer = null)
    {
        if (!_provider.IsConfigured)
            return LyPayServiceResult<LyPayTransferDto>.Fail("LyPay integration is not configured.", LyPayFailureKind.Unavailable);

        var now = DateTimeOffset.UtcNow;
        var staleBefore = now.AddSeconds(-StatusCheckClaimLeaseSeconds());
        var maxStatusChecks = Math.Max(1, _reconciliation.MaxStatusChecks);
        int claimed;

        if (manual)
        {
            claimed = await _db.LyPayTransfers
                .Where(x =>
                    x.Id == id &&
                    x.CompanyId == companyId &&
                    ((x.Status == LyPayTransferStatus.PendingProviderResponse &&
                        (x.ExecutedAt != null ||
                            (x.NextStatusCheckAt != null && x.NextStatusCheckAt <= now))) ||
                        x.Status == LyPayTransferStatus.Unknown) &&
                    (x.StatusCheckClaimedAt == null || x.StatusCheckClaimedAt < staleBefore))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.StatusCheckClaimedAt, now)
                    .SetProperty(x => x.UpdatedAt, now),
                    cancellationToken);
        }
        else
        {
            claimed = await _db.LyPayTransfers
                .Where(x =>
                    x.Id == id &&
                    x.CompanyId == companyId &&
                    x.Status == LyPayTransferStatus.PendingProviderResponse &&
                    x.StatusCheckAttempts < maxStatusChecks &&
                    (x.ExecutedAt != null ||
                        (x.NextStatusCheckAt != null && x.NextStatusCheckAt <= now)) &&
                    (x.StatusCheckClaimedAt == null || x.StatusCheckClaimedAt < staleBefore))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.StatusCheckClaimedAt, now)
                    .SetProperty(x => x.UpdatedAt, now)
                    .SetProperty(x => x.StatusCheckAttempts, x => x.StatusCheckAttempts + 1)
                    .SetProperty(
                        x => x.Status,
                        x => x.StatusCheckAttempts + 1 >= maxStatusChecks
                            ? LyPayTransferStatus.Unknown
                            : x.Status)
                    .SetProperty(
                        x => x.NextStatusCheckAt,
                        x => x.StatusCheckAttempts + 1 >= maxStatusChecks
                            ? null
                            : x.NextStatusCheckAt),
                    cancellationToken);
        }

        if (claimed != 1)
            return LyPayServiceResult<LyPayTransferDto>.Fail("A status check is already in progress.", LyPayFailureKind.Conflict);

        var transfer = await _db.LyPayTransfers.FirstAsync(x => x.Id == id, CancellationToken.None);
        var companyCode = await _db.Companies
            .Where(x => x.Id == companyId)
            .Select(x => x.Code)
            .FirstAsync(CancellationToken.None);
        var accountNoResult = DeriveProviderAccountNo(transfer.FromAccount, companyCode);
        if (!accountNoResult.Success)
        {
            transfer.StatusCheckClaimedAt = null;
            await _db.SaveChangesAsync(CancellationToken.None);
            return LyPayServiceResult<LyPayTransferDto>.Fail(accountNoResult.Error!, LyPayFailureKind.BadRequest);
        }

        var call = await _provider.CheckTransactionStatusAsync(
            new LyPayProviderStatusCommand(
                accountNoResult.Value!,
                transfer.Language,
                transfer.ReferenceNo,
                transfer.HostReference,
                transfer.DhbReference,
                transfer.AgreementReference),
            cancellationToken);

        var checkedAt = DateTimeOffset.UtcNow;
        transfer.ExecutedAt ??= transfer.ExecutionClaimedAt ?? checkedAt;
        transfer.LastStatusCheckedAt = checkedAt;
        transfer.StatusCheckClaimedAt = null;
        ApplyProviderMain(transfer, call.Main);

        var resolved =
            call.HttpSuccess &&
            call.Main?.Success == true &&
            call.Data?.IsFinished == true &&
            call.Data.IsTransactionSuccess.HasValue;
        if (resolved)
        {
            transfer.ProviderTransactionStatus = call.Data!.TransactionStatus?.Trim();
            transfer.NextStatusCheckAt = null;
            if (call.Data.IsTransactionSuccess == true)
            {
                transfer.Status = LyPayTransferStatus.Completed;
                transfer.CompletedAt = checkedAt;
            }
            else
            {
                transfer.Status = LyPayTransferStatus.Failed;
            }
        }
        else
        {
            if (call.Data is not null)
                transfer.ProviderTransactionStatus = call.Data.TransactionStatus?.Trim();
            if (!string.IsNullOrWhiteSpace(call.Error))
                transfer.ProviderReturnMessage = call.Error;

            if (transfer.Status == LyPayTransferStatus.PendingProviderResponse && !manual)
            {
                transfer.NextStatusCheckAt = checkedAt.AddSeconds(Math.Max(1, _reconciliation.RetryDelaySeconds));
            }
            else if (transfer.Status == LyPayTransferStatus.PendingProviderResponse)
            {
                // A manual inquiry neither consumes an automatic attempt nor
                // postpones an already scheduled worker poll.
                transfer.NextStatusCheckAt ??= checkedAt.AddSeconds(Math.Max(1, _reconciliation.RetryDelaySeconds));
            }
            else
            {
                // Manual inquiry did not resolve an already Unknown transaction.
                transfer.NextStatusCheckAt = null;
            }
        }

        try
        {
            await _db.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return LyPayServiceResult<LyPayTransferDto>.Fail("The transfer status changed during refresh.", LyPayFailureKind.Conflict);
        }

        return LyPayServiceResult<LyPayTransferDto>.Ok(MapTransfer(transfer, viewer));
    }

    private async Task PurgeValidationSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var consumedBefore = now.AddDays(-1);
        await _db.LyPayValidationSessions
            .Where(x => x.ExpiresAt < now || (x.ConsumedAt != null && x.ConsumedAt < consumedBefore))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static string? ValidateRequest(LyPayValidateTransferRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.FromAccount)) return "From account is required.";
        if (string.IsNullOrWhiteSpace(request.ToInstitutionId)) return "To bank is required.";
        if (string.IsNullOrWhiteSpace(request.ToAccount)) return "To account is required.";
        if (request.Amount <= 0) return "Amount must be greater than zero.";
        if (decimal.Round(request.Amount, 4) != request.Amount) return "Amount cannot contain more than four decimal places.";
        if (string.IsNullOrWhiteSpace(request.Description)) return "Narrative is required.";
        if (request.FromAccount.Trim().Length > 34) return "From account cannot exceed 34 characters.";
        if (request.ToInstitutionId.Trim().Length > 50) return "To bank reference is invalid.";
        if (request.Description.Trim().Length > 500) return "Narrative cannot exceed 500 characters.";
        if (!IsValidLibyanIban(NormalizeDestinationAccount(request.ToAccount)))
            return "Destination account must be a valid 25-character Libyan IBAN.";
        return null;
    }

    private static LyPayServiceResult<string> DeriveProviderAccountNo(string fromAccount, string companyCode)
    {
        var compact = fromAccount.Trim().Replace("-", string.Empty).Replace(" ", string.Empty);
        if (compact.Length != 13 || compact.Any(x => !char.IsDigit(x)))
            return LyPayServiceResult<string>.Fail("LyPay source account must be a complete 13-digit account.", LyPayFailureKind.BadRequest);

        var accountNo = compact.Substring(4, 6);
        if (!string.Equals(accountNo, companyCode.Trim(), StringComparison.Ordinal))
            return LyPayServiceResult<string>.Fail("The source account does not belong to this company.", LyPayFailureKind.BadRequest);

        return LyPayServiceResult<string>.Ok(accountNo);
    }

    private static string NormalizeDestinationAccount(string account) =>
        account.Trim()
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToUpperInvariant();

    private static bool IsValidLibyanIban(string iban)
    {
        if (iban.Length != 25 ||
            !iban.StartsWith("LY", StringComparison.Ordinal) ||
            iban.Skip(2).Any(character => character is < '0' or > '9'))
        {
            return false;
        }

        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var character in rearranged)
        {
            if (char.IsDigit(character))
            {
                remainder = (remainder * 10 + character - '0') % 97;
                continue;
            }

            var letterValue = character - 'A' + 10;
            remainder = (remainder * 10 + letterValue / 10) % 97;
            remainder = (remainder * 10 + letterValue % 10) % 97;
        }

        return remainder == 1;
    }

    private static string NormalizeLanguage(string language) =>
        string.Equals(language, "AR", StringComparison.OrdinalIgnoreCase) ? "AR" : "EN";

    private static string CreateReference(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private int ExecutionRecoveryDelaySeconds() => Math.Max(
        Math.Max(1, _reconciliation.RetryDelaySeconds),
        Math.Clamp(_options.TimeoutSeconds, 5, 120) + 30);

    private int StatusCheckClaimLeaseSeconds() => Math.Max(
        Math.Max(30, _reconciliation.ClaimLeaseSeconds),
        Math.Clamp(_options.TimeoutSeconds, 5, 120) + 30);

    private static string InstitutionDisplayName(LyPayInstitutionDto institution) =>
        !string.IsNullOrWhiteSpace(institution.FullName) ? institution.FullName : institution.ShortName;

    private static LyPayProviderDeviceInfo BuildDeviceInfo(
        string? trustedDeviceId,
        string? userAgent,
        LyPayClientDeviceInfoDto? clientInfo)
    {
        var ua = userAgent ?? string.Empty;
        var os = ua.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Android"
            : ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "iOS"
            : ua.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows"
            : ua.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) ? "macOS"
            : ua.Contains("Linux", StringComparison.OrdinalIgnoreCase) ? "Linux"
            : string.Empty;

        return new LyPayProviderDeviceInfo
        {
            DeviceId = trustedDeviceId?.Trim() ?? string.Empty,
            DeviceLat = Limit(clientInfo?.DeviceLat, 50),
            DeviceLon = Limit(clientInfo?.DeviceLon, 50),
            DeviceType = ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ? "mobile" : "web",
            Imei = string.Empty,
            OsType = os,
            OsVersion = string.Empty
        };
    }

    private static string Limit(string? value, int maximumLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
    }

    private static string ProviderError<T>(LyPayProviderCall<T> call, string fallback) where T : class
    {
        if (!string.IsNullOrWhiteSpace(call.Main?.ReturnMessage)) return call.Main.ReturnMessage.Trim();
        if (!string.IsNullOrWhiteSpace(call.Main?.GeneralError)) return call.Main.GeneralError.Trim();
        if (!string.IsNullOrWhiteSpace(call.Error)) return call.Error.Trim();
        return fallback;
    }

    private static void ApplyProviderMain(LyPayTransfer transfer, LyPayProviderMainResponse? main)
    {
        if (main is null) return;
        transfer.BankReferenceNo = main.BankReferenceNo?.Trim() ?? transfer.BankReferenceNo;
        transfer.ProviderReturnMessageCode = main.ReturnMessageCode?.Trim();
        transfer.ProviderReturnMessage = main.ReturnMessage?.Trim();
    }

    private LyPayTransferDto MapTransfer(
        LyPayTransfer transfer,
        LyPayCurrentUser? viewer = null) => new()
    {
        Id = transfer.Id,
        ReferenceNo = transfer.ReferenceNo,
        FromAccount = transfer.FromAccount,
        FromAccountName = transfer.FromAccountName,
        ToInstitutionId = transfer.ToInstitutionId,
        ToInstitutionName = transfer.ToInstitutionName,
        ToAccount = transfer.ToAccount,
        ToAccountName = transfer.ToAccountName,
        Amount = transfer.Amount,
        Currency = transfer.Currency,
        Description = transfer.Description,
        Status = transfer.Status,
        CreatedByName = transfer.CreatedByName,
        ApprovedByName = transfer.ApprovedByName,
        CreatedAt = transfer.CreatedAt,
        ValidatedAt = transfer.ValidatedAt,
        ExecutedAt = transfer.ExecutedAt,
        CompletedAt = transfer.CompletedAt,
        StatusCheckAttempts = transfer.StatusCheckAttempts,
        LastStatusCheckedAt = transfer.LastStatusCheckedAt,
        ProviderTransactionStatus = transfer.ProviderTransactionStatus,
        ProviderReturnMessageCode = transfer.ProviderReturnMessageCode,
        ProviderReturnMessage = transfer.ProviderReturnMessage,
        BankReferenceNo = transfer.BankReferenceNo,
        CanApprove = viewer is not null &&
            CanApprove(viewer) &&
            transfer.Status == LyPayTransferStatus.PendingApproval
    };
}
