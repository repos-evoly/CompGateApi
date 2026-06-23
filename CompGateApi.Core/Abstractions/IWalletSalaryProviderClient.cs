public interface IWalletSalaryProviderClient
{
    Task<WalletSalaryTransferResponseDto> PostSalaryWalletBatchAsync(
        WalletSalaryTransferRequestDto request,
        CancellationToken cancellationToken = default);

    Task<WalletSalaryStatusResponseDto> CheckSalaryWalletBatchStatusAsync(
        WalletSalaryStatusRequestDto request,
        CancellationToken cancellationToken = default);
}
