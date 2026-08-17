using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompGateApi.Core.OnePay;

public enum OnePayFailureKind
{
    BadRequest,
    Forbidden,
    NotFound,
    Conflict,
    Unavailable
}

public sealed record OnePayServiceResult<T>(
    bool Success,
    T? Value,
    string? Error,
    OnePayFailureKind? FailureKind)
{
    public static OnePayServiceResult<T> Ok(T value) => new(true, value, null, null);

    public static OnePayServiceResult<T> Fail(string error, OnePayFailureKind kind) =>
        new(false, default, error, kind);
}

public sealed class OnePayTransferService
{
    private const string MakerPermission = "canCreateTransfer";
    private const string CheckerPermission = "canposttransfer";

    private readonly CompGateApiDbContext _db;
    private readonly ITransferRequestRepository _transferRepository;
    private readonly IOnePayProviderClient _provider;
    private readonly IMemoryCache _cache;
    private readonly OnePayOptions _options;
    private readonly OnePayReconciliationOptions _reconciliation;
    private readonly ILogger<OnePayTransferService> _logger;

    public OnePayTransferService(
        CompGateApiDbContext db,
        ITransferRequestRepository transferRepository,
        IOnePayProviderClient provider,
        IMemoryCache cache,
        IOptions<OnePayOptions> options,
        IOptions<OnePayReconciliationOptions> reconciliation,
        ILogger<OnePayTransferService> logger)
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

    public bool CanCreate(OnePayCurrentUser user) => user.HasPermission(MakerPermission);

    public bool CanApprove(OnePayCurrentUser user) => user.HasPermission(CheckerPermission);

    public bool CanView(OnePayCurrentUser user) => CanCreate(user) || CanApprove(user);

    public async Task<OnePayCurrentUser?> GetCurrentUserAsync(
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

        return new OnePayCurrentUser
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

    public async Task<OnePayServiceResult<IReadOnlyList<OnePayAccountDto>>> GetAccountsAsync(
        OnePayCurrentUser user,
        CancellationToken cancellationToken)
    {
        if (!CanCreate(user))
            return OnePayServiceResult<IReadOnlyList<OnePayAccountDto>>.Fail("Permission denied.", OnePayFailureKind.Forbidden);

        try
        {
            var accounts = await _transferRepository.GetAccountsAsync(user.CompanyCode);
            var result = accounts
                .Where(x => !string.IsNullOrWhiteSpace(x.AccountString))
                .Select(x => new OnePayAccountDto
                {
                    AccountNumber = x.AccountString.Trim(),
                    AccountName = x.AccountName?.Trim() ?? string.Empty,
                    Currency = x.Currency?.Trim().ToUpperInvariant() ?? string.Empty,
                    AvailableBalance = x.AvailableBalance
                })
                .OrderBy(x => x.AccountNumber)
                .ToList();

            return OnePayServiceResult<IReadOnlyList<OnePayAccountDto>>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load OnePay source accounts for company {CompanyId}", user.CompanyId);
            return OnePayServiceResult<IReadOnlyList<OnePayAccountDto>>.Fail(
                "The company accounts could not be loaded.",
                OnePayFailureKind.Unavailable);
        }
    }

    public async Task<OnePayServiceResult<IReadOnlyList<OnePayInstitutionDto>>> GetInstitutionsAsync(
        OnePayCurrentUser user,
        string language,
        CancellationToken cancellationToken)
    {
        if (!CanCreate(user))
            return OnePayServiceResult<IReadOnlyList<OnePayInstitutionDto>>.Fail("Permission denied.", OnePayFailureKind.Forbidden);

        if (!_provider.IsConfigured)
        {
            return OnePayServiceResult<IReadOnlyList<OnePayInstitutionDto>>.Fail(
                "OnePay integration is not configured.",
                OnePayFailureKind.Unavailable);
        }

        var normalizedLanguage = NormalizeLanguage(language);
        var cacheKey = $"onepay:institutions:{user.CompanyCode}:{normalizedLanguage}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<OnePayInstitutionDto>? cached) && cached is not null)
            return OnePayServiceResult<IReadOnlyList<OnePayInstitutionDto>>.Ok(cached);

        var call = await _provider.GetInstitutionsAsync(
            user.CompanyCode,
            normalizedLanguage,
            CreateReference("OPI"),
            cancellationToken);

        if (!call.HttpSuccess || call.Main?.Success != true || call.Data is null)
        {
            return OnePayServiceResult<IReadOnlyList<OnePayInstitutionDto>>.Fail(
                ProviderError(call, "OnePay institutions could not be loaded."),
                OnePayFailureKind.Unavailable);
        }

        var institutions = (call.Data.Institutions ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.OnePayReference))
            .Select(x => new OnePayInstitutionDto
            {
                InstitutionId = x.OnePayReference!.Trim(),
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
            (IReadOnlyList<OnePayInstitutionDto>)institutions,
            TimeSpan.FromMinutes(Math.Max(1, _options.InstitutionCacheMinutes)));

        return OnePayServiceResult<IReadOnlyList<OnePayInstitutionDto>>.Ok(institutions);
    }

    public async Task<OnePayServiceResult<OnePayValidationDto>> ValidateAsync(
        OnePayCurrentUser user,
        OnePayValidateTransferRequestDto request,
        string? trustedDeviceId,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        if (!CanCreate(user))
            return OnePayServiceResult<OnePayValidationDto>.Fail("Permission denied.", OnePayFailureKind.Forbidden);

        if (!_provider.IsConfigured)
            return OnePayServiceResult<OnePayValidationDto>.Fail("OnePay integration is not configured.", OnePayFailureKind.Unavailable);

        if (request.BeneficiaryId.HasValue)
        {
            if (request.BeneficiaryId.Value <= 0)
                return OnePayServiceResult<OnePayValidationDto>.Fail("Beneficiary id is invalid.", OnePayFailureKind.BadRequest);

            var beneficiary = await _db.Beneficiaries
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.BeneficiaryId.Value &&
                    x.CompanyId == user.CompanyId &&
                    x.PaymentRail == PaymentRail.OnePay &&
                    !x.IsDeleted,
                    cancellationToken);
            if (beneficiary is null)
                return OnePayServiceResult<OnePayValidationDto>.Fail("OnePay beneficiary was not found.", OnePayFailureKind.NotFound);
            if (string.IsNullOrWhiteSpace(beneficiary.InstitutionId) || string.IsNullOrWhiteSpace(beneficiary.AccountNumber))
                return OnePayServiceResult<OnePayValidationDto>.Fail("The saved OnePay beneficiary destination is incomplete.", OnePayFailureKind.BadRequest);

            var savedInstitutionId = beneficiary.InstitutionId.Trim();
            var savedAccount = NormalizeDestinationAccount(beneficiary.AccountNumber);
            if (!string.IsNullOrWhiteSpace(request.ToInstitutionId) &&
                !string.Equals(request.ToInstitutionId.Trim(), savedInstitutionId, StringComparison.OrdinalIgnoreCase))
            {
                return OnePayServiceResult<OnePayValidationDto>.Fail(
                    "The selected bank does not match the saved beneficiary.",
                    OnePayFailureKind.BadRequest);
            }
            if (!string.IsNullOrWhiteSpace(request.ToAccount) &&
                !string.Equals(NormalizeDestinationAccount(request.ToAccount), savedAccount, StringComparison.Ordinal))
            {
                return OnePayServiceResult<OnePayValidationDto>.Fail(
                    "The destination account does not match the saved beneficiary.",
                    OnePayFailureKind.BadRequest);
            }

            request.ToInstitutionId = savedInstitutionId;
            request.ToAccount = savedAccount;
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return OnePayServiceResult<OnePayValidationDto>.Fail(validationError, OnePayFailureKind.BadRequest);

        var fromAccount = request.FromAccount.Trim();
        var toAccount = NormalizeDestinationAccount(request.ToAccount);
        var accountNoResult = DeriveProviderAccountNo(fromAccount, user.CompanyCode);
        if (!accountNoResult.Success)
            return OnePayServiceResult<OnePayValidationDto>.Fail(accountNoResult.Error!, OnePayFailureKind.BadRequest);

        OnePayAccountDto? sourceAccount;
        var accountsResult = await GetAccountsAsync(user, cancellationToken);
        if (!accountsResult.Success)
            return OnePayServiceResult<OnePayValidationDto>.Fail(accountsResult.Error!, accountsResult.FailureKind!.Value);

        sourceAccount = accountsResult.Value!
            .FirstOrDefault(x => string.Equals(x.AccountNumber, fromAccount, StringComparison.Ordinal));
        if (sourceAccount is null)
            return OnePayServiceResult<OnePayValidationDto>.Fail("The selected source account does not belong to this company.", OnePayFailureKind.BadRequest);
        if (string.IsNullOrWhiteSpace(sourceAccount.AccountName))
            return OnePayServiceResult<OnePayValidationDto>.Fail("The source account name could not be resolved.", OnePayFailureKind.Unavailable);
        if (string.IsNullOrWhiteSpace(sourceAccount.Currency))
            return OnePayServiceResult<OnePayValidationDto>.Fail("The source account currency could not be resolved.", OnePayFailureKind.Unavailable);

        var institutionsResult = await GetInstitutionsAsync(user, request.Language, cancellationToken);
        if (!institutionsResult.Success)
            return OnePayServiceResult<OnePayValidationDto>.Fail(institutionsResult.Error!, institutionsResult.FailureKind!.Value);

        var institution = institutionsResult.Value!
            .FirstOrDefault(x => string.Equals(x.InstitutionId, request.ToInstitutionId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (institution is null)
            return OnePayServiceResult<OnePayValidationDto>.Fail("The selected bank is not active for OnePay.", OnePayFailureKind.BadRequest);

        var phone = !string.IsNullOrWhiteSpace(user.CompanyPhone)
            ? user.CompanyPhone
            : user.UserPhone;
        if (string.IsNullOrWhiteSpace(phone))
            return OnePayServiceResult<OnePayValidationDto>.Fail("A company or user phone number is required for OnePay.", OnePayFailureKind.BadRequest);

        var language = NormalizeLanguage(request.Language);
        var referenceNo = CreateReference("OPV");
        var providerCall = await _provider.ValidateOutgoingTransferAsync(
            new OnePayProviderValidationCommand(
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
            return OnePayServiceResult<OnePayValidationDto>.Fail(
                ProviderError(providerCall, "OnePay rejected the transfer validation."),
                OnePayFailureKind.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(providerData.HostReference) ||
            string.IsNullOrWhiteSpace(providerData.DHBReference) ||
            string.IsNullOrWhiteSpace(providerData.AgreementReference) ||
            string.IsNullOrWhiteSpace(providerData.ToAccountName))
        {
            return OnePayServiceResult<OnePayValidationDto>.Fail(
                "OnePay returned an incomplete validation response.",
                OnePayFailureKind.Unavailable);
        }

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(Math.Max(1, _options.ValidationSessionMinutes));
        var session = new OnePayValidationSession
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
        _db.OnePayValidationSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        return OnePayServiceResult<OnePayValidationDto>.Ok(new OnePayValidationDto
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

    public async Task<OnePayServiceResult<OnePayTransferDto>> CreateAsync(
        OnePayCurrentUser user,
        Guid validationToken,
        CancellationToken cancellationToken)
    {
        if (!CanCreate(user))
            return OnePayServiceResult<OnePayTransferDto>.Fail("Permission denied.", OnePayFailureKind.Forbidden);
        if (validationToken == Guid.Empty)
            return OnePayServiceResult<OnePayTransferDto>.Fail("Validation token is required.", OnePayFailureKind.BadRequest);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var session = await _db.OnePayValidationSessions
                .FirstOrDefaultAsync(x =>
                    x.Id == validationToken &&
                    x.CompanyId == user.CompanyId &&
                    x.CreatedByUserId == user.UserId,
                    cancellationToken);

            if (session is null)
                return OnePayServiceResult<OnePayTransferDto>.Fail("Validation session was not found.", OnePayFailureKind.NotFound);
            if (session.ConsumedAt.HasValue)
                return OnePayServiceResult<OnePayTransferDto>.Fail("Validation session has already been used.", OnePayFailureKind.Conflict);
            if (session.ExpiresAt <= DateTimeOffset.UtcNow)
                return OnePayServiceResult<OnePayTransferDto>.Fail("Validation session has expired. Validate the transfer again.", OnePayFailureKind.Conflict);

            var transfer = new OnePayTransfer
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
                Status = OnePayTransferStatus.PendingApproval,
                HostReference = session.HostReference,
                DhbReference = session.DhbReference,
                AgreementReference = session.AgreementReference,
                BankReferenceNo = session.BankReferenceNo,
                ValidatedAt = session.ValidatedAt
            };

            session.ConsumedAt = DateTimeOffset.UtcNow;
            _db.OnePayTransfers.Add(transfer);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return OnePayServiceResult<OnePayTransferDto>.Ok(MapTransfer(transfer, user));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return OnePayServiceResult<OnePayTransferDto>.Fail("Validation session has already been used.", OnePayFailureKind.Conflict);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _logger.LogWarning(ex, "Concurrent or invalid OnePay validation-session consumption was blocked");
            return OnePayServiceResult<OnePayTransferDto>.Fail("Validation session could not be consumed.", OnePayFailureKind.Conflict);
        }
    }

    public async Task<PagedResult<OnePayTransferDto>> GetTransfersAsync(
        OnePayCurrentUser user,
        int page,
        int limit,
        string? searchTerm,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);
        var query = _db.OnePayTransfers
            .AsNoTracking()
            .Where(x => x.CompanyId == user.CompanyId);

        var search = searchTerm?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var isStatusSearch = Enum.TryParse<OnePayTransferStatus>(
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

        return new PagedResult<OnePayTransferDto>
        {
            Data = data,
            Page = page,
            Limit = limit,
            TotalRecords = total,
            TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)limit))
        };
    }

    public async Task<OnePayTransferDto?> GetTransferAsync(
        OnePayCurrentUser user,
        int id,
        CancellationToken cancellationToken)
    {
        var transfer = await _db.OnePayTransfers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id && x.CompanyId == user.CompanyId,
                cancellationToken);
        return transfer is null ? null : MapTransfer(transfer, user);
    }

    public async Task<OnePayServiceResult<OnePayTransferDto>> ApproveAndExecuteAsync(
        OnePayCurrentUser user,
        int id,
        CancellationToken cancellationToken)
    {
        if (!CanApprove(user))
            return OnePayServiceResult<OnePayTransferDto>.Fail("Permission denied.", OnePayFailureKind.Forbidden);
        if (!_provider.IsConfigured)
            return OnePayServiceResult<OnePayTransferDto>.Fail("OnePay integration is not configured.", OnePayFailureKind.Unavailable);

        var existing = await _db.OnePayTransfers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == user.CompanyId, cancellationToken);
        if (existing is null)
            return OnePayServiceResult<OnePayTransferDto>.Fail("OnePay transfer was not found.", OnePayFailureKind.NotFound);
        if (existing.Status != OnePayTransferStatus.PendingApproval)
            return OnePayServiceResult<OnePayTransferDto>.Fail("Only pending approval transfers can be executed.", OnePayFailureKind.Conflict);

        var accountNoResult = DeriveProviderAccountNo(existing.FromAccount, user.CompanyCode);
        if (!accountNoResult.Success)
            return OnePayServiceResult<OnePayTransferDto>.Fail(accountNoResult.Error!, OnePayFailureKind.BadRequest);

        var now = DateTimeOffset.UtcNow;
        var claimed = await _db.OnePayTransfers
            .Where(x =>
                x.Id == id &&
                x.CompanyId == user.CompanyId &&
                x.Status == OnePayTransferStatus.PendingApproval &&
                x.ExecutionClaimedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, OnePayTransferStatus.PendingProviderResponse)
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
            return OnePayServiceResult<OnePayTransferDto>.Fail("This transfer has already been claimed for execution.", OnePayFailureKind.Conflict);

        var transfer = await _db.OnePayTransfers.FirstAsync(x => x.Id == id, CancellationToken.None);
        var call = await _provider.ExecuteOutgoingTransferAsync(
            new OnePayProviderExecutionCommand(
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
        if (call.HttpSuccess && call.Main?.Success == true)
        {
            if (call.Main.StillUnderProcessing == true)
            {
                transfer.Status = OnePayTransferStatus.PendingProviderResponse;
                transfer.NextStatusCheckAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, _reconciliation.RetryDelaySeconds));
            }
            else
            {
                transfer.Status = OnePayTransferStatus.Completed;
                transfer.CompletedAt = DateTimeOffset.UtcNow;
                transfer.NextStatusCheckAt = null;
                transfer.ProviderTransactionStatus ??= "completed";
            }
        }
        else if (call.HttpSuccess && call.Main?.Success == false)
        {
            transfer.Status = OnePayTransferStatus.Failed;
            transfer.NextStatusCheckAt = null;
        }
        else
        {
            // A transport error or unreadable response is financially ambiguous.
            // Keep the one-time execution claim and reconcile only through status inquiry.
            transfer.Status = OnePayTransferStatus.PendingProviderResponse;
            transfer.ProviderReturnMessage = OptionalValue(call.Error, 1000);
            transfer.NextStatusCheckAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, _reconciliation.RetryDelaySeconds));
        }

        await _db.SaveChangesAsync(CancellationToken.None);
        return OnePayServiceResult<OnePayTransferDto>.Ok(MapTransfer(transfer, user));
    }

    public async Task<OnePayServiceResult<OnePayTransferDto>> RefreshStatusAsync(
        OnePayCurrentUser user,
        int id,
        CancellationToken cancellationToken)
    {
        if (!CanView(user))
            return OnePayServiceResult<OnePayTransferDto>.Fail("Permission denied.", OnePayFailureKind.Forbidden);

        var transfer = await _db.OnePayTransfers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == user.CompanyId, cancellationToken);
        if (transfer is null)
            return OnePayServiceResult<OnePayTransferDto>.Fail("OnePay transfer was not found.", OnePayFailureKind.NotFound);
        if (transfer.Status is not OnePayTransferStatus.PendingProviderResponse and not OnePayTransferStatus.Unknown)
            return OnePayServiceResult<OnePayTransferDto>.Fail("Only pending or unknown transfers can be refreshed.", OnePayFailureKind.Conflict);
        if (transfer.Status == OnePayTransferStatus.PendingProviderResponse &&
            transfer.ExecutedAt is null &&
            (transfer.NextStatusCheckAt is null || transfer.NextStatusCheckAt > DateTimeOffset.UtcNow))
        {
            return OnePayServiceResult<OnePayTransferDto>.Fail(
                "The transfer execution is still in progress.",
                OnePayFailureKind.Conflict);
        }

        return await RefreshStatusInternalAsync(id, user.CompanyId, true, cancellationToken, user);
    }

    public async Task ProcessPendingTransfersAsync(CancellationToken cancellationToken)
    {
        if (!_reconciliation.Enabled || !_provider.IsConfigured)
            return;

        var now = DateTimeOffset.UtcNow;
        var maxStatusChecks = Math.Max(1, _reconciliation.MaxStatusChecks);
        var ids = await _db.OnePayTransfers
            .AsNoTracking()
            .Where(x =>
                x.Status == OnePayTransferStatus.PendingProviderResponse &&
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
                _logger.LogError(ex, "OnePay reconciliation failed for transfer {TransferId}", item.Id);
            }
        }
    }

    private async Task<OnePayServiceResult<OnePayTransferDto>> RefreshStatusInternalAsync(
        int id,
        int companyId,
        bool manual,
        CancellationToken cancellationToken,
        OnePayCurrentUser? viewer = null)
    {
        if (!_provider.IsConfigured)
            return OnePayServiceResult<OnePayTransferDto>.Fail("OnePay integration is not configured.", OnePayFailureKind.Unavailable);

        var now = DateTimeOffset.UtcNow;
        var staleBefore = now.AddSeconds(-StatusCheckClaimLeaseSeconds());
        var maxStatusChecks = Math.Max(1, _reconciliation.MaxStatusChecks);
        int claimed;

        if (manual)
        {
            claimed = await _db.OnePayTransfers
                .Where(x =>
                    x.Id == id &&
                    x.CompanyId == companyId &&
                    ((x.Status == OnePayTransferStatus.PendingProviderResponse &&
                        (x.ExecutedAt != null ||
                            (x.NextStatusCheckAt != null && x.NextStatusCheckAt <= now))) ||
                        x.Status == OnePayTransferStatus.Unknown) &&
                    (x.StatusCheckClaimedAt == null || x.StatusCheckClaimedAt < staleBefore))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.StatusCheckClaimedAt, now)
                    .SetProperty(x => x.UpdatedAt, now),
                    cancellationToken);
        }
        else
        {
            claimed = await _db.OnePayTransfers
                .Where(x =>
                    x.Id == id &&
                    x.CompanyId == companyId &&
                    x.Status == OnePayTransferStatus.PendingProviderResponse &&
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
                            ? OnePayTransferStatus.Unknown
                            : x.Status)
                    .SetProperty(
                        x => x.NextStatusCheckAt,
                        x => x.StatusCheckAttempts + 1 >= maxStatusChecks
                            ? null
                            : x.NextStatusCheckAt),
                    cancellationToken);
        }

        if (claimed != 1)
            return OnePayServiceResult<OnePayTransferDto>.Fail("A status check is already in progress.", OnePayFailureKind.Conflict);

        var transfer = await _db.OnePayTransfers.FirstAsync(x => x.Id == id, CancellationToken.None);
        var companyCode = await _db.Companies
            .Where(x => x.Id == companyId)
            .Select(x => x.Code)
            .FirstAsync(CancellationToken.None);
        var accountNoResult = DeriveProviderAccountNo(transfer.FromAccount, companyCode);
        if (!accountNoResult.Success)
        {
            transfer.StatusCheckClaimedAt = null;
            await _db.SaveChangesAsync(CancellationToken.None);
            return OnePayServiceResult<OnePayTransferDto>.Fail(accountNoResult.Error!, OnePayFailureKind.BadRequest);
        }

        var call = await _provider.CheckTransactionStatusAsync(
            new OnePayProviderStatusCommand(
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
        if (call.Data is not null)
        {
            transfer.ProviderIsFinished = call.Data.IsFinished;
            transfer.ProviderIsTransactionSuccess = call.Data.IsTransactionSuccess;
            transfer.ProviderTransactionStatus = OptionalValue(call.Data.TransactionStatus, 1000);
        }

        var resolved =
            call.HttpSuccess &&
            call.Main?.Success == true &&
            call.Data?.IsFinished == true &&
            call.Data.IsTransactionSuccess.HasValue;
        if (resolved)
        {
            transfer.NextStatusCheckAt = null;
            if (call.Data!.IsTransactionSuccess == true)
            {
                transfer.Status = OnePayTransferStatus.Completed;
                transfer.CompletedAt = checkedAt;
            }
            else
            {
                transfer.Status = OnePayTransferStatus.Failed;
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(call.Error))
                transfer.ProviderReturnMessage = OptionalValue(call.Error, 1000);

            if (transfer.Status == OnePayTransferStatus.PendingProviderResponse && !manual)
            {
                transfer.NextStatusCheckAt = checkedAt.AddSeconds(Math.Max(1, _reconciliation.RetryDelaySeconds));
            }
            else if (transfer.Status == OnePayTransferStatus.PendingProviderResponse)
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
            return OnePayServiceResult<OnePayTransferDto>.Fail("The transfer status changed during refresh.", OnePayFailureKind.Conflict);
        }

        return OnePayServiceResult<OnePayTransferDto>.Ok(MapTransfer(transfer, viewer));
    }

    private async Task PurgeValidationSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var consumedBefore = now.AddDays(-1);
        await _db.OnePayValidationSessions
            .Where(x => x.ExpiresAt < now || (x.ConsumedAt != null && x.ConsumedAt < consumedBefore))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static string? ValidateRequest(OnePayValidateTransferRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.FromAccount)) return "From account is required.";
        if (string.IsNullOrWhiteSpace(request.ToInstitutionId)) return "To bank is required.";
        if (string.IsNullOrWhiteSpace(request.ToAccount)) return "To account is required.";
        if (request.Amount <= 0) return "Amount must be greater than zero.";
        if (decimal.Round(request.Amount, 4) != request.Amount) return "Amount cannot contain more than four decimal places.";
        if (string.IsNullOrWhiteSpace(request.Description)) return "Narrative is required.";
        if (request.FromAccount.Trim().Length > 34) return "From account cannot exceed 34 characters.";
        if (NormalizeDestinationAccount(request.ToAccount).Length > 34) return "To account cannot exceed 34 characters.";
        if (request.ToInstitutionId.Trim().Length > 50) return "To bank reference is invalid.";
        if (request.Description.Trim().Length > 500) return "Narrative cannot exceed 500 characters.";
        if (NormalizeDestinationAccount(request.ToAccount).Any(x => !char.IsLetterOrDigit(x))) return "To account can contain only letters and numbers.";
        return null;
    }

    private static OnePayServiceResult<string> DeriveProviderAccountNo(string fromAccount, string companyCode)
    {
        var compact = fromAccount.Trim().Replace("-", string.Empty).Replace(" ", string.Empty);
        if (compact.Length != 13 || compact.Any(x => !char.IsDigit(x)))
            return OnePayServiceResult<string>.Fail("OnePay source account must be a complete 13-digit account.", OnePayFailureKind.BadRequest);

        var accountNo = compact.Substring(4, 6);
        if (!string.Equals(accountNo, companyCode.Trim(), StringComparison.Ordinal))
            return OnePayServiceResult<string>.Fail("The source account does not belong to this company.", OnePayFailureKind.BadRequest);

        return OnePayServiceResult<string>.Ok(accountNo);
    }

    private static string NormalizeDestinationAccount(string account) =>
        account.Trim().Replace("-", string.Empty).Replace(" ", string.Empty);

    private static string NormalizeLanguage(string language) =>
        string.Equals(language, "AR", StringComparison.OrdinalIgnoreCase) ? "AR" : "EN";

    private static string CreateReference(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private int ExecutionRecoveryDelaySeconds() => Math.Max(
        Math.Max(1, _reconciliation.RetryDelaySeconds),
        Math.Clamp(_options.TimeoutSeconds, 5, 120) + 30);

    private int StatusCheckClaimLeaseSeconds() => Math.Max(
        Math.Max(30, _reconciliation.ClaimLeaseSeconds),
        Math.Clamp(_options.TimeoutSeconds, 5, 120) + 30);

    private static string InstitutionDisplayName(OnePayInstitutionDto institution) =>
        !string.IsNullOrWhiteSpace(institution.FullName) ? institution.FullName : institution.ShortName;

    private static OnePayProviderDeviceInfo BuildDeviceInfo(
        string? trustedDeviceId,
        string? userAgent,
        OnePayClientDeviceInfoDto? clientInfo)
    {
        var ua = userAgent ?? string.Empty;
        var os = ua.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Android"
            : ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "iOS"
            : ua.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows"
            : ua.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) ? "macOS"
            : ua.Contains("Linux", StringComparison.OrdinalIgnoreCase) ? "Linux"
            : string.Empty;

        return new OnePayProviderDeviceInfo
        {
            DeviceId = OptionalValue(trustedDeviceId),
            DeviceLat = OptionalValue(clientInfo?.DeviceLat, 50),
            DeviceLon = OptionalValue(clientInfo?.DeviceLon, 50),
            DeviceType = ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ? "mobile" : "web",
            Imei = null,
            OsType = OptionalValue(os),
            OsVersion = null
        };
    }

    private static string? OptionalValue(string? value, int? maximumLength = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        return maximumLength.HasValue && trimmed.Length > maximumLength.Value
            ? trimmed[..maximumLength.Value]
            : trimmed;
    }

    private static string ProviderError<T>(OnePayProviderCall<T> call, string fallback) where T : class
    {
        if (!string.IsNullOrWhiteSpace(call.Main?.ReturnMessage)) return call.Main.ReturnMessage.Trim();
        if (!string.IsNullOrWhiteSpace(call.Main?.GeneralError)) return call.Main.GeneralError.Trim();
        if (!string.IsNullOrWhiteSpace(call.Error)) return call.Error.Trim();
        return fallback;
    }

    private static void ApplyProviderMain(OnePayTransfer transfer, OnePayProviderMainResponse? main)
    {
        if (main is null) return;
        transfer.BankReferenceNo = OptionalValue(main.BankReferenceNo, 200) ?? transfer.BankReferenceNo;
        transfer.ProviderReturnMessageCode = OptionalValue(main.ReturnMessageCode, 100);
        transfer.ProviderReturnMessage = OptionalValue(main.ReturnMessage, 1000);
        transfer.ProviderGeneralError = OptionalValue(main.GeneralError, 1000);
        transfer.ProviderMainSuccess = main.Success;
        transfer.ProviderStillUnderProcessing = main.StillUnderProcessing;
    }

    private OnePayTransferDto MapTransfer(
        OnePayTransfer transfer,
        OnePayCurrentUser? viewer = null) => new()
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
        ProviderGeneralError = transfer.ProviderGeneralError,
        ProviderMainSuccess = transfer.ProviderMainSuccess,
        ProviderStillUnderProcessing = transfer.ProviderStillUnderProcessing,
        ProviderIsFinished = transfer.ProviderIsFinished,
        ProviderIsTransactionSuccess = transfer.ProviderIsTransactionSuccess,
        BankReferenceNo = transfer.BankReferenceNo,
        CanApprove = viewer is not null &&
            CanApprove(viewer) &&
            transfer.Status == OnePayTransferStatus.PendingApproval
    };
}
