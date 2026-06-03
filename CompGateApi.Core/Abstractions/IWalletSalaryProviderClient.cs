public interface IWalletSalaryProviderClient
{
    Task<WalletSalaryTransferResponseDto> PostSalaryWalletBatchAsync(
        WalletSalaryTransferRequestDto request,
        CancellationToken cancellationToken = default);
}
