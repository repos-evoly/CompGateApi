using System.Net.Http.Json;
using System.Text.Json;
using AutoMapper;
using CompGateApi.Core.Dtos;
using CompGateApi.Core.Errors;
using CompGateApi.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompGateApi.Core.WalletSalary;

public enum WalletSalaryItemOutcome
{
    Completed,
    Failed,
    Pending,
    Unknown
}

public sealed class WalletSalaryService
{
    public const string CreateOrEditPermission = "canCreateOrEditSalaryCycle";
    public const string PostPermission = "canPostSalaryCycle";
    public const string EmployeeManagementPermission = "CompanyCanEmployees";

    private readonly CompGateApiDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWalletSalaryProviderClient _provider;
    private readonly WalletSalaryOptions _options;
    private readonly IMapper _mapper;
    private readonly ILogger<WalletSalaryService> _logger;

    public WalletSalaryService(
        CompGateApiDbContext db,
        IHttpClientFactory httpClientFactory,
        IWalletSalaryProviderClient provider,
        IOptions<WalletSalaryOptions> options,
        IMapper mapper,
        ILogger<WalletSalaryService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _provider = provider;
        _options = options.Value;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(
        int userId,
        string permission,
        CancellationToken cancellationToken = default) =>
        await _db.UserRolePermissions
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == userId && x.Permission.NameEn == permission,
                cancellationToken);

    public async Task<SalaryWalletCapabilitiesDto> GetCapabilitiesAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var permissions = await _db.UserRolePermissions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.Permission.NameEn)
            .ToListAsync(cancellationToken);
        var permissionSet = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new SalaryWalletCapabilitiesDto
        {
            AllocationPrecision = 3,
            CommissionMode = _options.CommissionMode,
            CanCreateOrEdit = permissionSet.Contains(CreateOrEditPermission),
            CanPost = permissionSet.Contains(PostPermission),
            Rails = new Dictionary<string, SalaryWalletRailCapabilityDto>
            {
                [WalletSalaryChannels.BankAccount] = new()
                {
                    Enabled = true,
                    Configured = true,
                    Available = true,
                    RecipientTypes = new() { WalletSalaryRecipientTypes.AccountNumber }
                },
                [WalletSalaryChannels.EvoWallet] = ToCapability(_options.Evo),
                [WalletSalaryChannels.BcdWallet] = ToCapability(_options.Bcd)
            }
        };
    }

    public void ValidateWalletPreflight(SalaryCycle cycle)
    {
        var walletAllocations = cycle.Entries
            .SelectMany(x => x.Allocations)
            .Where(x => WalletSalaryChannels.Wallets.Contains(x.PaymentChannel))
            .ToList();
        if (walletAllocations.Count == 0) return;

        if (!string.Equals(cycle.Currency, "LYD", StringComparison.OrdinalIgnoreCase))
            throw new PayrollException("Wallet salary payments currently support LYD only.");
        if (!string.Equals(_options.CommissionMode, "receiverPaid", StringComparison.OrdinalIgnoreCase))
            throw new PayrollException(
                "Wallet salary posting is blocked because this flow requires receiver-paid provider commission configuration.");
        if (cycle.DebitAccount.Trim().Length != 13 || !cycle.DebitAccount.Trim().All(char.IsDigit))
            throw new PayrollException("Salary debit account must contain exactly 13 digits.");

        foreach (var rail in walletAllocations.Select(x => x.PaymentChannel).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var options = _options.ForRail(rail);
            if (!options.Enabled)
                throw new PayrollException($"{rail} wallet salary payments are not enabled.");
            if (!options.IsConfigured)
                throw new PayrollException(
                    $"{rail} wallet salary payments are not configured. Base URL, API key and full 13-digit shadow account are required.");
        }
    }

    public async Task<IReadOnlyList<SalaryWalletBatch>> PrepareInitialBatchesAsync(
        SalaryCycle cycle,
        int postedBy,
        CancellationToken cancellationToken = default)
    {
        var prepared = new List<SalaryWalletBatch>();
        foreach (var rail in new[] { WalletSalaryChannels.EvoWallet, WalletSalaryChannels.BcdWallet })
        {
            var allocations = cycle.Entries
                .SelectMany(x => x.Allocations)
                .Where(x => string.Equals(x.PaymentChannel, rail, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Id)
                .ToList();
            if (allocations.Count == 0) continue;

            var existing = cycle.WalletBatches.FirstOrDefault(x =>
                string.Equals(x.Rail, rail, StringComparison.OrdinalIgnoreCase) &&
                x.Purpose == "initial" &&
                x.SequenceNumber == 1);
            if (existing is not null)
            {
                prepared.Add(existing);
                continue;
            }

            var request = new WalletSalaryProviderBatchRequest
            {
                ClientReference = $"SAL-{Guid.NewGuid():N}",
                Items = allocations.Select(x => WalletSalaryProviderContract.CreateRequestItem(
                    x.Amount,
                    x.RecipientType,
                    x.Destination)).ToList()
            };
            var exactJson = WalletSalaryProviderContract.SerializeBatch(request);
            var batch = new SalaryWalletBatch
            {
                SalaryCycleId = cycle.Id,
                SalaryCycle = cycle,
                Rail = rail,
                SequenceNumber = 1,
                Purpose = "initial",
                ShadowAccount = _options.ForRail(rail).ShadowAccount.Trim(),
                Status = "prepared",
                RequestedTotalAmount = allocations.Sum(x => x.Amount),
                ProviderRequestJson = exactJson,
                ProviderRequestHash = WalletSalaryProviderContract.HashPayload(exactJson),
                DispatchClassification = "notDispatched",
                RetryAllowed = false,
                Items = allocations.Select((allocation, index) => new SalaryWalletBatchItem
                {
                    SalaryEntryAllocationId = allocation.Id,
                    SalaryEntryAllocation = allocation,
                    RequestIndex = index,
                    Amount = allocation.Amount,
                    Destination = allocation.Destination,
                    RecipientType = allocation.RecipientType,
                    Status = "pending"
                }).ToList()
            };
            _db.SalaryWalletBatches.Add(batch);
            cycle.WalletBatches.Add(batch);
            prepared.Add(batch);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return prepared;
    }

    public async Task ProcessInitialBatchAsync(
        SalaryWalletBatch batch,
        SalaryCycle cycle,
        int postedBy,
        CancellationToken cancellationToken = default)
    {
        if (batch.Status is "completed" or "partiallyCompleted" or "failed" or "unknown" or "pendingProvider")
            return;

        if (batch.CoreFundingStatus != "funded")
        {
            var funded = await FundShadowAsync(batch, cycle, cancellationToken);
            if (!funded) return;
        }

        await DispatchProviderBatchAsync(batch, postedBy, cancellationToken);
    }

    public async Task<SalaryWalletBatchDto?> RefreshBatchAsync(
        int companyId,
        int cycleId,
        int batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await LoadBatchAsync(companyId, cycleId, batchId, cancellationToken);
        if (batch is null) return null;
        if (!IsRefreshableStatus(batch.Status))
            throw new PayrollException("Only pending or unknown wallet batches can be refreshed.");
        if (string.IsNullOrWhiteSpace(batch.ProviderBatchId))
            throw new PayrollException("This wallet batch has no provider batch ID and cannot be refreshed.");

        var call = await _provider.GetBatchAsync(batch.Rail, batch.ProviderBatchId, cancellationToken);
        batch.LastCheckedAt = DateTimeOffset.UtcNow;
        batch.CheckClaimedAt = null;
        if (call.Data is not null && call.HttpSuccess)
        {
            ApplyProviderResponse(batch, call.Data, call.ResponseRaw, postedBy: null);
        }
        else
        {
            batch.LastErrorCode = call.ErrorCode;
            batch.LastError = call.Error;
            if (batch.Status == "pendingProvider")
                batch.NextCheckAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(10, _options.ReconciliationDelaySeconds));
        }

        await _db.SaveChangesAsync(cancellationToken);
        await UpdateCycleStateAsync(batch.SalaryCycleId, cancellationToken);
        return ToBatchDto(batch);
    }

    public async Task<SalaryWalletBatchDto?> RetryBatchAsync(
        int companyId,
        int cycleId,
        int batchId,
        int postedBy,
        CancellationToken cancellationToken = default)
    {
        var batch = await LoadBatchAsync(companyId, cycleId, batchId, cancellationToken);
        if (batch is null) return null;
        if (!batch.RetryAllowed)
            throw new PayrollException("This wallet batch cannot be retried because the previous request may have reached a financial system.");

        var coreFundingStatus = batch.CoreFundingStatus;
        if (coreFundingStatus is not ("failed" or "notStarted" or "funded"))
            throw new PayrollException("This batch is at an uncertain core-funding boundary and cannot be retried.");

        // Claim the retry in one conditional SQL update. Only one concurrent
        // caller can change RetryAllowed from true to false, so a double-click
        // cannot advance two requests to the financial dispatch code.
        var now = DateTimeOffset.UtcNow;
        var retryQuery = _db.SalaryWalletBatches.Where(x =>
            x.Id == batchId &&
            x.SalaryCycleId == cycleId &&
            x.SalaryCycle.CompanyId == companyId &&
            x.RetryAllowed &&
            x.CoreFundingStatus == coreFundingStatus);
        int claimed;
        if (coreFundingStatus == "failed")
        {
            claimed = await retryQuery.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RetryAllowed, false)
                .SetProperty(x => x.LastError, (string?)null)
                .SetProperty(x => x.LastErrorCode, (string?)null)
                .SetProperty(x => x.Status, "retryClaimed")
                .SetProperty(x => x.CoreFundingStatus, "notStarted")
                .SetProperty(x => x.CoreFundingReference, (string?)null)
                .SetProperty(x => x.DispatchClassification, "notDispatched")
                .SetProperty(x => x.DispatchStartedAt, (DateTimeOffset?)null)
                .SetProperty(x => x.DispatchCompletedAt, (DateTimeOffset?)null)
                .SetProperty(x => x.UpdatedAt, now),
                cancellationToken);
        }
        else
        {
            var nextStatus = coreFundingStatus == "funded" ? "funded" : "retryClaimed";
            claimed = await retryQuery.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RetryAllowed, false)
                .SetProperty(x => x.LastError, (string?)null)
                .SetProperty(x => x.LastErrorCode, (string?)null)
                .SetProperty(x => x.Status, nextStatus)
                .SetProperty(x => x.DispatchClassification, "notDispatched")
                .SetProperty(x => x.DispatchStartedAt, (DateTimeOffset?)null)
                .SetProperty(x => x.DispatchCompletedAt, (DateTimeOffset?)null)
                .SetProperty(x => x.UpdatedAt, now),
                cancellationToken);
        }

        if (claimed != 1)
            throw new PayrollException("This wallet batch retry was already claimed or is no longer safe.");

        _db.ChangeTracker.Clear();
        batch = await LoadBatchAsync(companyId, cycleId, batchId, cancellationToken);
        if (batch is null) return null;

        await ProcessInitialBatchAsync(batch, batch.SalaryCycle, postedBy, cancellationToken);
        await UpdateCycleStateAsync(batch.SalaryCycleId, cancellationToken);
        return ToBatchDto(batch);
    }

    public async Task<int> ReconcilePendingBatchesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await MarkStalePostingCyclesUnknownAsync(now, cancellationToken);
        var claimExpiry = now.AddSeconds(-Math.Max(30, _options.ReconciliationClaimSeconds));
        var ids = await _db.SalaryWalletBatches
            .AsNoTracking()
            .Where(x =>
                x.Status == "pendingProvider" &&
                x.ProviderBatchId != null &&
                (x.NextCheckAt == null || x.NextCheckAt <= now) &&
                (x.CheckClaimedAt == null || x.CheckClaimedAt < claimExpiry))
            .OrderBy(x => x.NextCheckAt)
            .Select(x => x.Id)
            .Take(Math.Clamp(_options.ReconciliationBatchSize, 1, 100))
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var id in ids)
        {
            var claimed = await _db.SalaryWalletBatches
                .Where(x =>
                    x.Id == id &&
                    x.Status == "pendingProvider" &&
                    (x.CheckClaimedAt == null || x.CheckClaimedAt < claimExpiry))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.CheckClaimedAt, now),
                    cancellationToken);
            if (claimed != 1) continue;

            _db.ChangeTracker.Clear();
            var batch = await _db.SalaryWalletBatches
                .Include(x => x.Items).ThenInclude(x => x.SalaryEntryAllocation)
                .Include(x => x.ManualReviews)
                .FirstAsync(x => x.Id == id, cancellationToken);
            var call = await _provider.GetBatchAsync(batch.Rail, batch.ProviderBatchId!, cancellationToken);
            batch.LastCheckedAt = DateTimeOffset.UtcNow;
            batch.CheckClaimedAt = null;
            if (call.Data is not null && call.HttpSuccess)
                ApplyProviderResponse(batch, call.Data, call.ResponseRaw, postedBy: null);
            else
            {
                batch.LastErrorCode = call.ErrorCode;
                batch.LastError = call.Error;
                batch.NextCheckAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(10, _options.ReconciliationDelaySeconds));
            }
            await _db.SaveChangesAsync(cancellationToken);
            await UpdateCycleStateAsync(batch.SalaryCycleId, cancellationToken);
            processed++;
        }

        return processed;
    }

    private async Task MarkStalePostingCyclesUnknownAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var staleBefore = now.AddSeconds(-Math.Max(120, _options.ReconciliationClaimSeconds));
        // An edit claim is only a database mutation lock; it never crosses a
        // financial boundary, so a stale claim is safe to release.
        await _db.SalaryCycles
            .Where(x => x.PostingStatus == "editing" &&
                        x.PostedAt == null &&
                        x.PostingClaimedAt != null &&
                        x.PostingClaimedAt < staleBefore)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.PostingStatus, "notPosted")
                .SetProperty(x => x.PostingClaimedAt, (DateTimeOffset?)null),
                cancellationToken);

        var staleBatches = await _db.SalaryWalletBatches
            .Include(x => x.Items).ThenInclude(x => x.SalaryEntryAllocation)
            .Include(x => x.ManualReviews)
            .Where(x =>
                (x.Status == "retryClaimed" || x.Status == "funding" ||
                 x.Status == "funded" || x.Status == "dispatching") &&
                x.UpdatedAt < staleBefore)
            .Take(20)
            .ToListAsync(cancellationToken);
        var affectedCycleIds = new HashSet<int>();
        foreach (var batch in staleBatches)
        {
            if (batch.Status == "retryClaimed" &&
                batch.CoreFundingStatus == "notStarted" &&
                batch.DispatchStartedAt is null &&
                string.IsNullOrWhiteSpace(batch.CoreFundingReference) &&
                string.IsNullOrWhiteSpace(batch.ProviderBatchId))
            {
                // The retry claim was persisted, but the process stopped before
                // either financial boundary. Re-expose one deliberate retry.
                batch.Status = "failedBeforeDispatch";
                batch.DispatchClassification = "notDispatched";
                batch.RetryAllowed = true;
                batch.LastErrorCode = "STALE_RETRY_BEFORE_FUNDING";
                batch.LastError = "The safe retry stopped before shadow funding began. It can be claimed again.";
                foreach (var item in batch.Items)
                {
                    item.Status = "failedBeforeDispatch";
                    item.SalaryEntryAllocation.Status = "failedBeforeDispatch";
                    item.SalaryEntryAllocation.TransferResultCode = batch.LastErrorCode;
                    item.SalaryEntryAllocation.TransferResultReason = batch.LastError;
                }
            }
            else if (batch.Status == "funded" && batch.CoreFundingStatus == "funded" &&
                batch.DispatchStartedAt is null)
            {
                // Core success was proven and persisted, while no provider
                // dispatch boundary was crossed. A manual retry may therefore
                // dispatch the saved provider payload without funding again.
                batch.Status = "failedBeforeDispatch";
                batch.DispatchClassification = "notDispatched";
                batch.RetryAllowed = true;
                batch.LastErrorCode = "STALE_AFTER_CONFIRMED_FUNDING";
                batch.LastError = "Shadow funding completed, but provider dispatch did not start. An authorized provider-only retry is safe.";
                foreach (var item in batch.Items)
                {
                    item.Status = "failedBeforeDispatch";
                    item.SalaryEntryAllocation.Status = "failedBeforeDispatch";
                    item.SalaryEntryAllocation.TransferResultCode = batch.LastErrorCode;
                    item.SalaryEntryAllocation.TransferResultReason = batch.LastError;
                }
            }
            else
            {
                MarkUnknown(
                    batch,
                    "STALE_FINANCIAL_DISPATCH",
                    "The application stopped while a financial call may have been in progress. Automatic retry is disabled.");
            }
            affectedCycleIds.Add(batch.SalaryCycleId);
        }

        var cycles = await _db.SalaryCycles
            .Include(x => x.Entries).ThenInclude(x => x.Allocations)
            .Include(x => x.WalletBatches).ThenInclude(x => x.Items).ThenInclude(x => x.SalaryEntryAllocation)
            .Include(x => x.WalletBatches).ThenInclude(x => x.ManualReviews)
            .Where(x =>
                x.PostingStatus == "posting" &&
                x.PostingClaimedAt != null &&
                x.PostingClaimedAt < staleBefore)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var cycle in cycles)
        {
            if (CanResetWholeCycleBeforeAnyFinancialBoundary(cycle))
            {
                ResetWholeCycleBeforeAnyFinancialBoundary(cycle);
                continue;
            }

            // A prepared batch has not crossed either the core-funding or
            // provider-dispatch boundary. If another channel already posted,
            // expose only a deliberate retry for this batch; never reset the
            // whole cycle and risk reposting the completed channel.
            MarkPreparedBatchesSafeToRetry(cycle);

            var hasUncertainFinancialBoundary = cycle.Entries
                    .SelectMany(x => x.Allocations)
                    .Any(x => x.Status is "funding" or "posting" or "dispatching") ||
                cycle.WalletBatches.Any(x => x.Status is "funding" or "dispatching") ||
                cycle.BankFeeStatus == "dispatching";
            if (!hasUncertainFinancialBoundary)
            {
                cycle.PostingClaimedAt = null;
                affectedCycleIds.Add(cycle.Id);
                continue;
            }

            cycle.PostingStatus = "unknown";
            cycle.PostingClaimedAt = null;
            cycle.PostingCompletedAt = now;
            if (cycle.BankFeeStatus == "dispatching")
            {
                cycle.BankFeeStatus = "unknown";
                cycle.BankFeeError = "The application stopped while fixed-fee settlement may have been in progress. Its outcome requires manual review.";
            }
            foreach (var allocation in cycle.Entries.SelectMany(x => x.Allocations)
                         .Where(x => x.Status is "posting" or "funding" or "dispatching"))
            {
                allocation.Status = "unknown";
                allocation.TransferResultCode = "STALE_POSTING_CLAIM";
                allocation.TransferResultReason = "The application restarted or stopped while a financial call may have been in progress.";
            }
            foreach (var batch in cycle.WalletBatches.Where(x =>
                         x.Status is "funding" or "dispatching"))
            {
                MarkUnknown(
                    batch,
                    "STALE_POSTING_CLAIM",
                    "The application stopped while a financial call may have been in progress. Automatic retry is disabled.");
            }
        }

        if (cycles.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);
        else if (staleBatches.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        foreach (var cycleId in affectedCycleIds)
            await UpdateCycleStateAsync(cycleId, cancellationToken);

        await RecoverDeterministicUnknownCyclesAsync(cancellationToken);
    }

    private async Task RecoverDeterministicUnknownCyclesAsync(CancellationToken cancellationToken)
    {
        var cycles = await _db.SalaryCycles
            .Include(x => x.Entries).ThenInclude(x => x.Allocations)
            .Include(x => x.WalletBatches).ThenInclude(x => x.Items).ThenInclude(x => x.SalaryEntryAllocation)
            .Where(x => x.PostingStatus == "unknown")
            .Take(20)
            .ToListAsync(cancellationToken);

        var affectedCycleIds = new List<int>();
        foreach (var cycle in cycles)
        {
            if (CanResetWholeCycleBeforeAnyFinancialBoundary(cycle))
            {
                ResetWholeCycleBeforeAnyFinancialBoundary(cycle);
                continue;
            }

            MarkPreparedBatchesSafeToRetry(cycle);
            var hasUncertainBoundary = cycle.BankFeeStatus is "unknown" or "dispatching" ||
                cycle.Entries.SelectMany(x => x.Allocations).Any(x =>
                    x.Status is "unknown" or "posting" or "funding" or "dispatching") ||
                cycle.WalletBatches.Any(x =>
                    x.Status is "unknown" or "funding" or "dispatching");
            if (!hasUncertainBoundary)
                affectedCycleIds.Add(cycle.Id);
        }

        if (cycles.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        foreach (var cycleId in affectedCycleIds)
            await UpdateCycleStateAsync(cycleId, cancellationToken);
    }

    private static bool CanResetWholeCycleBeforeAnyFinancialBoundary(SalaryCycle cycle)
    {
        var allocations = cycle.Entries.SelectMany(x => x.Allocations).ToList();
        return allocations.Count > 0 &&
               allocations.All(x => x.Status == "pending") &&
               cycle.WalletBatches.All(x =>
                   x.Status == "prepared" &&
                   x.CoreFundingStatus == "notStarted" &&
                   x.DispatchStartedAt == null &&
                   string.IsNullOrWhiteSpace(x.CoreFundingReference) &&
                   string.IsNullOrWhiteSpace(x.ProviderBatchId)) &&
               string.IsNullOrWhiteSpace(cycle.BankReference) &&
               string.IsNullOrWhiteSpace(cycle.BankFeeReference);
    }

    private static void ResetWholeCycleBeforeAnyFinancialBoundary(SalaryCycle cycle)
    {
        cycle.PostingStatus = "notPosted";
        cycle.PostingClaimedAt = null;
        cycle.PostingStartedAt = null;
        cycle.PostingCompletedAt = null;
        cycle.PostedAt = null;
        cycle.PostedByUserId = null;
    }

    private static void MarkPreparedBatchesSafeToRetry(SalaryCycle cycle)
    {
        foreach (var prepared in cycle.WalletBatches.Where(x =>
                     x.Status == "prepared" &&
                     x.CoreFundingStatus == "notStarted" &&
                     x.DispatchStartedAt == null &&
                     string.IsNullOrWhiteSpace(x.CoreFundingReference) &&
                     string.IsNullOrWhiteSpace(x.ProviderBatchId)))
        {
            prepared.Status = "failedBeforeDispatch";
            prepared.DispatchClassification = "notDispatched";
            prepared.RetryAllowed = true;
            prepared.LastErrorCode = "STALE_BEFORE_FUNDING";
            prepared.LastError = "Posting stopped before shadow funding began. An authorized retry is safe.";
            foreach (var item in prepared.Items.Where(x => x.Status == "pending"))
            {
                item.Status = "failedBeforeDispatch";
                item.SalaryEntryAllocation.Status = "failedBeforeDispatch";
                item.SalaryEntryAllocation.TransferResultCode = prepared.LastErrorCode;
                item.SalaryEntryAllocation.TransferResultReason = prepared.LastError;
            }
        }
    }

    private async Task UpdateCycleStateAsync(int cycleId, CancellationToken cancellationToken)
    {
        var cycle = await _db.SalaryCycles
            .Include(x => x.Entries).ThenInclude(x => x.Allocations)
            .FirstOrDefaultAsync(x => x.Id == cycleId, cancellationToken);
        if (cycle is null) return;

        var allocations = cycle.Entries.SelectMany(x => x.Allocations).ToList();
        foreach (var entry in cycle.Entries)
        {
            entry.IsTransferred = entry.Allocations.Count > 0 && entry.Allocations.All(x => x.Status == "completed");
            if (entry.IsTransferred) entry.TransferredAt ??= DateTime.UtcNow;
        }

        if (cycle.BankFeeStatus == "dispatching")
            cycle.PostingStatus = "posting";
        else if (cycle.BankFeeStatus == "unknown" || allocations.Any(x => x.Status == "unknown"))
            cycle.PostingStatus = "unknown";
        else if (cycle.BankFeeStatus == "failed" && allocations.Any(x => x.Status == "completed"))
            cycle.PostingStatus = "partiallyCompleted";
        else if (allocations.Any(x => x.Status is "pending" or "posting" or "funding" or "funded" or "dispatching" or "pendingProvider"))
            cycle.PostingStatus = "pendingProvider";
        else if (allocations.Count > 0 && allocations.All(x => x.Status == "completed"))
            cycle.PostingStatus = "completed";
        else if (allocations.Any(x => x.Status == "completed"))
            cycle.PostingStatus = "partiallyCompleted";
        else
            cycle.PostingStatus = "failed";

        if (cycle.PostedAt is null && cycle.PostingStatus != "notPosted")
            cycle.PostedAt = DateTime.UtcNow;
        cycle.TotalAmount = allocations.Where(x => x.Status == "completed").Sum(x => x.Amount);
        cycle.PostingCompletedAt = cycle.PostingStatus is "pendingProvider" or "posting"
            ? null
            : DateTimeOffset.UtcNow;
        cycle.PostingClaimedAt = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public static WalletSalaryItemOutcome ClassifyProviderItemStatus(string? status)
    {
        var normalized = status?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "approved" or "success" or "completed" => WalletSalaryItemOutcome.Completed,
            "failed" or "declined" or "rejected" or "error" => WalletSalaryItemOutcome.Failed,
            "pending" or "processing" or "accepted" or "inprogress" or "in_progress" => WalletSalaryItemOutcome.Pending,
            _ => WalletSalaryItemOutcome.Unknown
        };
    }

    public static bool IsRefreshableStatus(string? status) =>
        status is "pendingProvider" or "unknown";

    public SalaryWalletBatchDto ToBatchDto(SalaryWalletBatch batch)
    {
        var dto = _mapper.Map<SalaryWalletBatchDto>(batch);
        dto.CanRetry = batch.RetryAllowed;
        dto.OutcomeKnown = batch.Status is not "unknown" and not "dispatching";
        dto.RequiresManualReview = batch.Status is "unknown" or "failed" or "partiallyCompleted" ||
                                   batch.ManualReviews.Any(x => x.Status == "open" &&
                                       !(batch.Status == "completed" && IsProviderOutcomeReview(x.ReasonCode)));
        dto.CanRefreshStatus = !string.IsNullOrWhiteSpace(batch.ProviderBatchId) &&
                               IsRefreshableStatus(batch.Status);
        return dto;
    }

    public SalaryPostingSummaryDto BuildSummary(SalaryCycle cycle)
    {
        var allocations = cycle.Entries.SelectMany(x => x.Allocations).ToList();
        return new SalaryPostingSummaryDto
        {
            OverallStatus = cycle.PostingStatus,
            BankAccount = Summarize(allocations, WalletSalaryChannels.BankAccount),
            EvoWallet = Summarize(allocations, WalletSalaryChannels.EvoWallet),
            BcdWallet = Summarize(allocations, WalletSalaryChannels.BcdWallet)
        };
    }

    private async Task<bool> FundShadowAsync(
        SalaryWalletBatch batch,
        SalaryCycle cycle,
        CancellationToken cancellationToken)
    {
        if (batch.CoreFundingStatus is "dispatching" or "unknown") return false;

        var reference = $"WSF{cycle.Id}{batch.Id}{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        if (reference.Length > 32) reference = reference[..32];
        batch.CoreFundingReference = reference;
        batch.CoreFundingStatus = "dispatching";
        batch.Status = "funding";
        batch.RetryAllowed = false;
        foreach (var item in batch.Items)
            item.SalaryEntryAllocation.Status = "funding";
        await _db.SaveChangesAsync(cancellationToken);

        var source = cycle.DebitAccount.Trim();
        var shadow = batch.ShadowAccount.Trim();
        const int scale = 1000;
        var amountValue = ((long)(Math.Round(
            batch.RequestedTotalAmount,
            3,
            MidpointRounding.AwayFromZero) * scale)).ToString("D15");
        var detailId = CoreBatchDetailId.Create(reference, 1);
        var payload = new
        {
            Header = new
            {
                system = "CompanyGateway",
                referenceId = reference,
                userName = "CompanyGateway",
                customerNumber = source.Substring(4, 6),
                language = "AR"
            },
            Details = new Dictionary<string, object>
            {
                ["@UNIT"] = "LIV",
                ["@HID"] = reference,
                ["@TYPE"] = "D",
                ["@FORCPAY"] = "N",
                ["@ACCOUNT"] = source,
                ["@TRFAMT"] = amountValue,
                ["@TRFCCY"] = cycle.Currency,
                ["@DTCD"] = "021",
                ["@CTCD"] = "521",
                ["@TRFREF"] = reference,
                ["@NR1"] = $"Wallet salary funding {batch.Rail}",
                ["@NR2"] = cycle.SalaryMonth,
                ["@NR3"] = string.Empty,
                ["@NR4"] = string.Empty,
                ["JournalEntries"] = new[]
                {
                    new Dictionary<string, string>
                    {
                        ["YBCD10DID"] = detailId,
                        ["YBCD10ACC"] = shadow,
                        ["YBCD10AMT"] = amountValue,
                        ["YBCD10CCY"] = cycle.Currency,
                        ["YBCD10NR1"] = $"Wallet salary funding {batch.Rail}",
                        ["YBCD10NR2"] = cycle.SalaryMonth,
                        ["YBCD10NR3"] = string.Empty,
                        ["YBCD10NR4"] = string.Empty
                    }
                }
            }
        };

        try
        {
            var client = _httpClientFactory.CreateClient("BankApi");
            var uri = new Uri(client.BaseAddress!, "api/mobile/PostBatchApply");
            using var response = await client.PostAsJsonAsync(uri, payload, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            batch.CoreFundingResponseRaw = raw;

            if (response.IsSuccessStatusCode &&
                IsConfirmedCoreSuccess(raw, shadow, detailId, amountValue))
            {
                batch.CoreFundingStatus = "funded";
                batch.Status = "funded";
                foreach (var item in batch.Items)
                    item.SalaryEntryAllocation.Status = "funded";
                await _db.SaveChangesAsync(cancellationToken);
                return true;
            }

            if (response.IsSuccessStatusCode &&
                IsExplicitCoreRejection(raw, shadow, detailId, amountValue))
            {
                batch.CoreFundingStatus = "failed";
                batch.Status = "fundingFailed";
                batch.RetryAllowed = true;
                batch.LastErrorCode = "CORE_FUNDING_REJECTED";
                batch.LastError = "Core banking rejected wallet shadow funding.";
                foreach (var item in batch.Items)
                {
                    item.SalaryEntryAllocation.Status = "fundingFailed";
                    item.SalaryEntryAllocation.TransferResultCode = batch.LastErrorCode;
                    item.SalaryEntryAllocation.TransferResultReason = batch.LastError;
                }
            }
            else
            {
                MarkUnknown(batch, "CORE_FUNDING_UNKNOWN", "Wallet shadow funding may have reached core banking, so it cannot be repeated automatically.");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            _logger.LogWarning(ex, "Wallet salary core funding outcome is unknown for batch {BatchId}", batch.Id);
            MarkUnknown(batch, "CORE_FUNDING_TRANSPORT_UNKNOWN", "Wallet shadow funding outcome is unknown and cannot be repeated automatically.");
        }

        await _db.SaveChangesAsync(cancellationToken);
        return false;
    }

    private async Task DispatchProviderBatchAsync(
        SalaryWalletBatch batch,
        int postedBy,
        CancellationToken cancellationToken)
    {
        WalletSalaryProviderBatchRequest request;
        try
        {
            request = JsonSerializer.Deserialize<WalletSalaryProviderBatchRequest>(
                batch.ProviderRequestJson,
                WalletSalaryProviderContract.JsonOptions)
                ?? throw new JsonException("Empty wallet salary batch payload.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Stored wallet salary payload is invalid for batch {BatchId}", batch.Id);
            batch.Status = "failedBeforeDispatch";
            batch.DispatchClassification = "notDispatched";
            batch.RetryAllowed = false;
            batch.LastErrorCode = "STORED_PAYLOAD_INVALID";
            batch.LastError = "Stored wallet salary request is invalid and was not dispatched.";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var reconstructedRequest = new WalletSalaryProviderBatchRequest
        {
            ClientReference = request.ClientReference,
            Items = batch.Items
                .OrderBy(x => x.RequestIndex)
                .Select(x => WalletSalaryProviderContract.CreateRequestItem(
                    x.Amount,
                    x.RecipientType,
                    x.Destination))
                .ToList()
        };
        var reconstructedJson = WalletSalaryProviderContract.SerializeBatch(reconstructedRequest);
        var storedHash = WalletSalaryProviderContract.HashPayload(batch.ProviderRequestJson);
        if (!string.Equals(storedHash, batch.ProviderRequestHash, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(reconstructedJson, batch.ProviderRequestJson, StringComparison.Ordinal))
        {
            batch.Status = "failedBeforeDispatch";
            batch.DispatchClassification = "notDispatched";
            batch.RetryAllowed = false;
            batch.LastErrorCode = "PROVIDER_REQUEST_INTEGRITY_FAILED";
            batch.LastError = "The immutable wallet salary request no longer matches its saved recipients, amounts, order, or hash. It was not dispatched.";
            foreach (var item in batch.Items)
            {
                item.Status = "failedBeforeDispatch";
                item.SalaryEntryAllocation.Status = "failedBeforeDispatch";
                item.SalaryEntryAllocation.TransferResultCode = batch.LastErrorCode;
                item.SalaryEntryAllocation.TransferResultReason = batch.LastError;
            }
            AddManualReview(batch, batch.LastErrorCode, batch.LastError);
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var attempt = new SalaryWalletBatchAttempt
        {
            SalaryWalletBatchId = batch.Id,
            AttemptNumber = batch.Attempts.Count + 1,
            AttemptType = "dispatch",
            DispatchClassification = "outcomeUnknown",
            ResultStatus = "started",
            StartedAt = DateTimeOffset.UtcNow
        };
        batch.Attempts.Add(attempt);
        batch.DispatchStartedAt = attempt.StartedAt;
        batch.Status = "dispatching";
        batch.DispatchClassification = "outcomeUnknown";
        batch.RetryAllowed = false;
        foreach (var item in batch.Items)
            item.SalaryEntryAllocation.Status = "dispatching";
        await _db.SaveChangesAsync(cancellationToken);

        var call = await _provider.SendBatchAsync(batch.Rail, batch.ProviderRequestJson, cancellationToken);
        attempt.CompletedAt = DateTimeOffset.UtcNow;
        attempt.DispatchClassification = ToDispatchString(call.DispatchClassification);
        attempt.Error = call.Error;
        batch.DispatchClassification = attempt.DispatchClassification;
        batch.DispatchCompletedAt = attempt.CompletedAt;
        batch.ProviderResponseJson = call.ResponseRaw;
        batch.LastErrorCode = call.ErrorCode;
        batch.LastError = call.Error;

        if (call.DispatchClassification == WalletSalaryDispatchClassification.NotDispatched)
        {
            attempt.ResultStatus = "failedBeforeDispatch";
            batch.Status = "failedBeforeDispatch";
            batch.RetryAllowed = true;
            foreach (var item in batch.Items)
            {
                item.SalaryEntryAllocation.Status = "failedBeforeDispatch";
                item.SalaryEntryAllocation.TransferResultCode = call.ErrorCode;
                item.SalaryEntryAllocation.TransferResultReason = call.Error;
            }
        }
        else if (call.DispatchClassification == WalletSalaryDispatchClassification.OutcomeUnknown)
        {
            attempt.ResultStatus = "unknown";
            MarkUnknown(batch, call.ErrorCode ?? "PROVIDER_OUTCOME_UNKNOWN", call.Error ?? "Wallet provider outcome is unknown.");
        }
        else if (call.HttpSuccess && call.Data is not null && !string.IsNullOrWhiteSpace(call.Data.BatchId))
        {
            attempt.ResultStatus = "responseReceived";
            ApplyProviderResponse(batch, call.Data, call.ResponseRaw, postedBy);
        }
        else
        {
            // A response proves dispatch reached the provider. The supplied
            // contract does not define any rejection code as safe to replay.
            attempt.ResultStatus = "providerRejectedOrUnreadable";
            MarkUnknown(
                batch,
                call.ErrorCode ?? "PROVIDER_RESPONSE_UNUSABLE",
                call.Error ?? "Wallet provider response did not contain a batch ID. The request cannot be replayed safely.");
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private void ApplyProviderResponse(
        SalaryWalletBatch batch,
        WalletSalaryProviderBatchResponse response,
        string? raw,
        int? postedBy)
    {
        // Preserve the provider's reconciliation key even when the remainder of
        // the initial response is malformed. The exact key is then required on
        // every GET response before any financial result is accepted.
        if (string.IsNullOrWhiteSpace(batch.ProviderBatchId) &&
            !string.IsNullOrWhiteSpace(response.BatchId))
            batch.ProviderBatchId = response.BatchId;

        // batchQueue acknowledges acceptance before individual results exist.
        // Only accept the observed empty pending envelope for this funded batch.
        if (string.Equals(response.Status, "pending", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(response.BatchId) &&
            response.BatchId == batch.ProviderBatchId &&
            batch.CoreFundingStatus == "funded" &&
            response.Total == batch.Items.Count && response.Total > 0 &&
            response.Items is { Count: 0 } &&
            response.Approved == 0 && response.Failed == 0 &&
            response.TotalAmount == 0m && response.Commission == 0m &&
            response.SenderCommission == 0m && response.ReceiverCommission == 0m &&
            response.DebitAmount == 0m &&
            batch.Items.All(x => x.Status != "completed" && !x.SalaryEntryAllocation.IsTransferred))
        {
            batch.Status = "pendingProvider";
            batch.ProviderResponseJson = raw;
            batch.RetryAllowed = false;
            batch.LastErrorCode = null;
            batch.LastError = null;
            batch.NextCheckAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(10, _options.ReconciliationDelaySeconds));
            batch.ReversalStatus = "notRequired";
            batch.ReversalAmount = 0m;
            foreach (var item in batch.Items)
            {
                item.Status = "pendingProvider";
                item.SalaryEntryAllocation.Status = "pendingProvider";
                item.SalaryEntryAllocation.TransferResultCode = "pending";
                item.SalaryEntryAllocation.TransferResultReason = null;
                item.SalaryEntryAllocation.ReversalStatus = "notRequired";
            }
            ResolveProviderOutcomeReviews(batch, "Provider confirmed that the batch is pending. Status polling will continue.");
            return;
        }

        if (!HasExactProviderItemMapping(batch, response, out var mappingError))
        {
            MarkUnknown(batch, "PROVIDER_ITEM_MAPPING_INVALID", mappingError);
            batch.ProviderResponseJson = raw;
            return;
        }

        batch.ProviderResponseJson = raw;
        batch.SuccessfulTotalAmount = 0m;
        batch.FailedTotalAmount = 0m;
        batch.TotalCommission = response.Commission;
        batch.SenderCommission = response.SenderCommission;
        batch.ReceiverCommission = response.ReceiverCommission;
        batch.DebitAmount = response.DebitAmount;
        batch.RetryAllowed = false;
        batch.LastError = null;
        batch.LastErrorCode = null;

        var outcomes = new List<WalletSalaryItemOutcome>();
        foreach (var providerItem in response.Items)
        {
            var item = batch.Items.SingleOrDefault(x => x.RequestIndex == providerItem.Index);
            if (item is null)
            {
                outcomes.Add(WalletSalaryItemOutcome.Unknown);
                continue;
            }

            var outcome = ClassifyProviderItemStatus(providerItem.Status);
            outcomes.Add(outcome);
            var status = outcome switch
            {
                WalletSalaryItemOutcome.Completed => "completed",
                WalletSalaryItemOutcome.Failed => "failed",
                WalletSalaryItemOutcome.Pending => "pendingProvider",
                _ => "unknown"
            };
            item.Status = status;
            item.CommissionAmount = providerItem.Commission;
            item.ProviderTransactionId = providerItem.TransactionId;
            item.ProviderTxCode = providerItem.TxCode;
            item.ProviderResponseRaw = JsonSerializer.Serialize(providerItem, WalletSalaryProviderContract.JsonOptions);

            var allocation = item.SalaryEntryAllocation;
            allocation.Status = status;
            allocation.CommissionAmount = providerItem.Commission;
            allocation.ProviderTransactionId = providerItem.TransactionId;
            allocation.ProviderTxCode = providerItem.TxCode;
            allocation.ProviderResponseRaw = item.ProviderResponseRaw;
            allocation.PostedByUserId ??= postedBy;
            if (outcome == WalletSalaryItemOutcome.Completed)
            {
                allocation.IsTransferred = true;
                allocation.TransferredAt ??= DateTimeOffset.UtcNow;
                allocation.TransferResultCode = "approved";
                allocation.TransferResultReason = null;
                allocation.ReversalStatus = "notRequired";
                allocation.ReversalReference = null;
                allocation.ReversalError = null;
                batch.SuccessfulTotalAmount += item.Amount;
            }
            else if (outcome == WalletSalaryItemOutcome.Failed)
            {
                allocation.IsTransferred = false;
                allocation.TransferResultCode = providerItem.Status;
                var reason = string.IsNullOrWhiteSpace(providerItem.Error)
                    ? "Wallet provider rejected this salary allocation." : providerItem.Error.Trim();
                allocation.TransferResultReason = reason[..Math.Min(reason.Length, 1024)];
                allocation.ReversalStatus = "manualReview";
                batch.FailedTotalAmount += item.Amount;
            }
            else if (outcome == WalletSalaryItemOutcome.Unknown)
            {
                allocation.TransferResultCode = "UNKNOWN_PROVIDER_STATUS";
                allocation.TransferResultReason = $"Unrecognized provider status: {providerItem.Status ?? "(empty)"}.";
                allocation.ReversalStatus = "manualReview";
            }
        }

        if (outcomes.Any(x => x == WalletSalaryItemOutcome.Failed))
        {
            var reasons = string.Join("; ", batch.Items
                .Where(x => x.Status == "failed")
                .Select(x => x.SalaryEntryAllocation.TransferResultReason)
                .Distinct());
            batch.LastError = reasons[..Math.Min(reasons.Length, 1024)];
            batch.LastErrorCode = "PROVIDER_TRANSFER_FAILED";
        }

        if (outcomes.Count > 0 && outcomes.All(x => x == WalletSalaryItemOutcome.Completed))
        {
            batch.Status = "completed";
            batch.NextCheckAt = null;
            batch.ReversalStatus = "notRequired";
            batch.ReversalAmount = 0m;
            ResolveProviderOutcomeReviews(batch, "Provider confirmed all payments completed with matching recipients and amounts.");
        }
        else if (outcomes.Any(x => x == WalletSalaryItemOutcome.Unknown))
        {
            batch.Status = "unknown";
            batch.NextCheckAt = null;
            batch.ReversalStatus = "manualReview";
            batch.ReversalAmount = batch.Items.Where(x => x.Status != "completed").Sum(x => x.Amount);
            AddManualReview(batch, "UNKNOWN_PROVIDER_STATUS", "The provider returned an unknown or incomplete item status. No retry or reversal is allowed.");
        }
        else if (outcomes.Any(x => x == WalletSalaryItemOutcome.Pending))
        {
            batch.Status = "pendingProvider";
            batch.NextCheckAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(10, _options.ReconciliationDelaySeconds));
            batch.ReversalStatus = "notRequired";
            batch.ReversalAmount = 0m;
        }
        else if (outcomes.All(x => x == WalletSalaryItemOutcome.Failed))
        {
            ResolveProviderOutcomeReviews(batch, "Provider confirmed all payments failed; funded amounts still require review.");
            batch.Status = "failed";
            batch.NextCheckAt = null;
            batch.ReversalStatus = "manualReview";
            batch.ReversalAmount = batch.Items.Sum(x => x.Amount);
            AddManualReview(batch, "FAILED_FUNDS_REQUIRE_REVIEW", "Provider-declined amounts require manual review; automatic reversal is disabled until the reversal contract is confirmed.");
        }
        else
        {
            batch.Status = "partiallyCompleted";
            batch.NextCheckAt = null;
            batch.ReversalStatus = "manualReview";
            batch.ReversalAmount = batch.Items.Where(x => x.Status != "completed").Sum(x => x.Amount);
            AddManualReview(batch, "PARTIAL_RESULT_REQUIRES_REVIEW", "Failed wallet salary amounts require manual review; completed items must not be resent.");
        }
    }

    private async Task<SalaryWalletBatch?> LoadBatchAsync(
        int companyId,
        int cycleId,
        int batchId,
        CancellationToken cancellationToken) =>
        await _db.SalaryWalletBatches
            .Include(x => x.SalaryCycle)
            .Include(x => x.Items).ThenInclude(x => x.SalaryEntryAllocation)
            .Include(x => x.Attempts)
            .Include(x => x.ManualReviews)
            .FirstOrDefaultAsync(
                x => x.Id == batchId &&
                     x.SalaryCycleId == cycleId &&
                     x.SalaryCycle.CompanyId == companyId,
                cancellationToken);

    private void MarkUnknown(SalaryWalletBatch batch, string code, string message)
    {
        batch.Status = "unknown";
        batch.RetryAllowed = false;
        batch.LastErrorCode = code;
        batch.LastError = message;
        batch.NextCheckAt = null;
        foreach (var item in batch.Items)
        {
            if (item.Status == "completed" || item.SalaryEntryAllocation.Status == "completed")
                continue;
            item.Status = "unknown";
            item.SalaryEntryAllocation.Status = "unknown";
            item.SalaryEntryAllocation.TransferResultCode = code;
            item.SalaryEntryAllocation.TransferResultReason = message;
            item.SalaryEntryAllocation.ReversalStatus = "manualReview";
        }
        batch.ReversalStatus = "manualReview";
        batch.ReversalAmount = batch.Items.Where(x => x.Status != "completed").Sum(x => x.Amount);
        AddManualReview(batch, code, message);
    }

    private static bool IsProviderOutcomeReview(string reasonCode) => reasonCode is
        "PROVIDER_ITEM_MAPPING_INVALID" or "UNKNOWN_PROVIDER_STATUS" or
        "PROVIDER_OUTCOME_UNKNOWN" or "PROVIDER_RESPONSE_UNUSABLE" or
        "REQUEST_TIMEOUT" or "TRANSPORT_FAILURE" or "RESPONSE_READ_FAILURE";

    private static void ResolveProviderOutcomeReviews(SalaryWalletBatch batch, string note)
    {
        foreach (var review in batch.ManualReviews.Where(x => x.Status == "open" && IsProviderOutcomeReview(x.ReasonCode)))
        {
            review.Status = "resolved";
            review.ResolvedAt = DateTimeOffset.UtcNow;
            review.ResolutionNote = note;
        }
    }

    private static void AddManualReview(SalaryWalletBatch batch, string reasonCode, string reason)
    {
        if (batch.ManualReviews.Any(x => x.Status == "open" && x.ReasonCode == reasonCode)) return;
        batch.ManualReviews.Add(new SalaryWalletManualReview
        {
            ReasonCode = reasonCode,
            Reason = reason,
            Status = "open"
        });
    }

    private static bool IsConfirmedCoreSuccess(
        string raw,
        string expectedAccount,
        string expectedDetailId,
        string expectedAmount)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            if (TryGetPropertyIgnoreCase(root, "Details", out var details) &&
                TryGetPropertyIgnoreCase(details, "Lines", out var lines) &&
                lines.ValueKind == JsonValueKind.Array)
            {
                foreach (var line in lines.EnumerateArray())
                {
                    var response = GetStringIgnoreCase(line, "YBCD10RESP");
                    var account = GetStringIgnoreCase(line, "YBCD10ACC");
                    var detailId = GetStringIgnoreCase(line, "YBCD10DID");
                    var amount = GetStringIgnoreCase(line, "YBCD10AMT");
                    if (string.Equals(response, "S", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(account?.Trim(), expectedAccount, StringComparison.Ordinal) &&
                        string.Equals(detailId?.Trim(), expectedDetailId, StringComparison.Ordinal) &&
                        string.Equals(amount?.Trim(), expectedAmount, StringComparison.Ordinal))
                        return true;
                }
            }

        }
        catch (JsonException)
        {
        }
        return false;
    }

    private static bool IsExplicitCoreRejection(
        string raw,
        string expectedAccount,
        string expectedDetailId,
        string expectedAmount)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            if (!TryGetPropertyIgnoreCase(root, "Details", out var details) ||
                !TryGetPropertyIgnoreCase(details, "Lines", out var lines) ||
                lines.ValueKind != JsonValueKind.Array)
                return false;
            foreach (var line in lines.EnumerateArray())
            {
                var account = GetStringIgnoreCase(line, "YBCD10ACC");
                var response = GetStringIgnoreCase(line, "YBCD10RESP");
                var detailId = GetStringIgnoreCase(line, "YBCD10DID");
                var amount = GetStringIgnoreCase(line, "YBCD10AMT");
                if (string.Equals(account?.Trim(), expectedAccount, StringComparison.Ordinal) &&
                    string.Equals(detailId?.Trim(), expectedDetailId, StringComparison.Ordinal) &&
                    string.Equals(amount?.Trim(), expectedAmount, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(response) &&
                    !string.Equals(response, "S", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private bool HasExactProviderItemMapping(
        SalaryWalletBatch batch,
        WalletSalaryProviderBatchResponse response,
        out string error)
    {
        if (response.Items is null)
        {
            error = "Provider response does not contain an item list.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(batch.ProviderBatchId) &&
            !string.Equals(response.BatchId, batch.ProviderBatchId, StringComparison.Ordinal))
        {
            error = "Provider batch ID does not match the batch being reconciled.";
            return false;
        }

        if (response.Items.Count != batch.Items.Count)
        {
            error = "Provider item count does not match the dispatched batch.";
            return false;
        }

        if (response.Total != response.Items.Count)
        {
            error = "Provider total item count does not match the returned items.";
            return false;
        }

        // BCD reports zero executed totals when every item failed without a debit.
        // Keep exact recipient/amount/count validation below for this response too.
        var failedWithoutDebit = response.Items.Count > 0 &&
            string.Equals(response.Status, "completed", StringComparison.OrdinalIgnoreCase) &&
            response.Items.All(x => ClassifyProviderItemStatus(x.Status) == WalletSalaryItemOutcome.Failed) &&
            response.TotalAmount == 0m && response.DebitAmount == 0m &&
            response.Commission == 0m && response.SenderCommission == 0m && response.ReceiverCommission == 0m;

        if (!failedWithoutDebit && response.TotalAmount != batch.Items.Sum(x => x.Amount))
        {
            error = "Provider total amount does not match the dispatched batch.";
            return false;
        }

        if (response.Total < 0 || response.Approved < 0 || response.Failed < 0 ||
            response.TotalAmount < 0m || response.Commission < 0m ||
            response.SenderCommission < 0m || response.ReceiverCommission < 0m ||
            response.DebitAmount < 0m)
        {
            error = "Provider response contains negative aggregate values.";
            return false;
        }

        if (!string.Equals(_options.CommissionMode, "receiverPaid", StringComparison.OrdinalIgnoreCase) ||
            response.SenderCommission != 0m ||
            (!failedWithoutDebit && response.DebitAmount != batch.RequestedTotalAmount))
        {
            error = "Provider commission/debit totals do not match the configured receiver-paid funding model.";
            return false;
        }

        if (response.Commission != response.Items.Sum(x => x.Commission) ||
            response.ReceiverCommission != response.Commission)
        {
            error = "Provider commission aggregates do not match the returned item commissions.";
            return false;
        }

        var duplicateIndex = response.Items.GroupBy(x => x.Index).FirstOrDefault(x => x.Count() != 1);
        if (duplicateIndex is not null)
        {
            error = $"Provider returned duplicate item index {duplicateIndex.Key}.";
            return false;
        }

        foreach (var expected in batch.Items)
        {
            var actual = response.Items.SingleOrDefault(x => x.Index == expected.RequestIndex);
            if (actual is null)
            {
                error = $"Provider omitted item index {expected.RequestIndex}.";
                return false;
            }
            if (actual.Amount != expected.Amount)
            {
                error = $"Provider amount mismatch at item index {expected.RequestIndex}.";
                return false;
            }

            if (actual.Commission < 0m || actual.Commission > actual.Amount)
            {
                error = $"Provider commission is invalid at item index {expected.RequestIndex}.";
                return false;
            }

            var actualDestination = expected.RecipientType == WalletSalaryRecipientTypes.EvoId
                ? actual.EvoId
                : actual.PhoneNumber;
            var unexpectedDestination = expected.RecipientType == WalletSalaryRecipientTypes.EvoId
                ? actual.PhoneNumber
                : actual.EvoId;
            if (!string.Equals(actualDestination?.Trim(), expected.Destination, StringComparison.Ordinal))
            {
                error = $"Provider recipient mismatch at item index {expected.RequestIndex}.";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(unexpectedDestination))
            {
                error = $"Provider returned multiple recipient identifiers at item index {expected.RequestIndex}.";
                return false;
            }
        }

        var classified = response.Items
            .Select(x => ClassifyProviderItemStatus(x.Status))
            .ToList();
        if (response.Approved != classified.Count(x => x == WalletSalaryItemOutcome.Completed) ||
            response.Failed != classified.Count(x => x == WalletSalaryItemOutcome.Failed))
        {
            error = "Provider approved/failed totals do not match the returned item statuses.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            value = property.Value;
            return true;
        }
        value = default;
        return false;
    }

    private static string? GetStringIgnoreCase(JsonElement element, string name) =>
        TryGetPropertyIgnoreCase(element, name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;

    private static SalaryPostingChannelSummaryDto Summarize(
        IEnumerable<SalaryEntryAllocation> allocations,
        string channel)
    {
        var selected = allocations
            .Where(x => string.Equals(x.PaymentChannel, channel, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return new SalaryPostingChannelSummaryDto
        {
            Total = selected.Count,
            Completed = selected.Count(x => x.Status == "completed"),
            Failed = selected.Count(x => x.Status is "failed" or "fundingFailed" or "failedBeforeDispatch"),
            Pending = selected.Count(x => x.Status is "pending" or "funding" or "funded" or "dispatching" or "pendingProvider"),
            Unknown = selected.Count(x => x.Status == "unknown"),
            Amount = selected.Sum(x => x.Amount),
            Commission = selected.Sum(x => x.CommissionAmount)
        };
    }

    private static SalaryWalletRailCapabilityDto ToCapability(WalletSalaryRailOptions options) => new()
    {
        Enabled = options.Enabled,
        Configured = options.IsConfigured,
        Available = options.Enabled && options.IsConfigured,
        RecipientTypes = new() { WalletSalaryRecipientTypes.EvoId, WalletSalaryRecipientTypes.PhoneNumber }
    };

    private static string ToDispatchString(WalletSalaryDispatchClassification value) => value switch
    {
        WalletSalaryDispatchClassification.NotDispatched => "notDispatched",
        WalletSalaryDispatchClassification.ResponseReceived => "responseReceived",
        _ => "outcomeUnknown"
    };

}
