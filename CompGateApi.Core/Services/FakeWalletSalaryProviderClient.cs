public class FakeWalletSalaryProviderClient : IWalletSalaryProviderClient
{
    public Task<WalletSalaryTransferResponseDto> PostSalaryWalletBatchAsync(
        WalletSalaryTransferRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var processedAt = DateTime.UtcNow;
        var results = request.Items.Select((item, index) =>
        {
            var isSuccess = index % 2 == 0;
            var commission = isSuccess ? Math.Round(item.Amount * 0.002m, 3) : 0m;
            var upperChannel = request.WalletChannel.ToUpperInvariant();

            return new WalletSalaryTransferResultDto
            {
                ClientReference = item.ClientReference,
                SalaryCycleId = item.SalaryCycleId,
                SalaryEntryId = item.SalaryEntryId,
                EmployeeId = item.EmployeeId,
                WalletId = item.WalletId,
                Amount = item.Amount,
                Commission = commission,
                Currency = item.Currency,
                Status = isSuccess ? "success" : "failed",
                StatusCode = isSuccess ? "SUCCESS" : "WALLET_NOT_FOUND",
                StatusMessage = isSuccess ? "Wallet transfer completed" : "Wallet does not exist",
                ProviderTransactionId = isSuccess ? $"{upperChannel}-TXN-{processedAt:yyyyMMddHHmmss}-{index + 1:000}" : null,
                ProcessedAt = processedAt
            };
        }).ToList();

        var successfulTotal = results
            .Where(r => string.Equals(r.Status, "success", StringComparison.OrdinalIgnoreCase))
            .Sum(r => r.Amount);
        var failedTotal = results
            .Where(r => string.Equals(r.Status, "failed", StringComparison.OrdinalIgnoreCase))
            .Sum(r => r.Amount);
        var totalCommission = results
            .Where(r => string.Equals(r.Status, "success", StringComparison.OrdinalIgnoreCase))
            .Sum(r => r.Commission);

        var overallStatus = failedTotal == 0m
            ? "success"
            : successfulTotal == 0m
                ? "failed"
                : "partial_success";

        var response = new WalletSalaryTransferResponseDto
        {
            BatchReference = request.BatchReference,
            CoreReferenceId = request.CoreReferenceId,
            WalletChannel = request.WalletChannel,
            OverallStatus = overallStatus,
            Currency = request.Currency,
            RequestedTotalAmount = request.RequestedTotalAmount,
            SuccessfulTotalAmount = successfulTotal,
            FailedTotalAmount = failedTotal,
            TotalCommission = totalCommission,
            Results = results,
            TraceId = $"FAKE-{request.WalletChannel.ToUpperInvariant()}-{Guid.NewGuid():N}"
        };

        return Task.FromResult(response);
    }
}
