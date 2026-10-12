using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompGateApi.Core.WalletSalary;

public sealed class WalletSalaryReconciliationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WalletSalaryOptions _options;
    private readonly ILogger<WalletSalaryReconciliationWorker> _logger;

    public WalletSalaryReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<WalletSalaryOptions> options,
        ILogger<WalletSalaryReconciliationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ReconciliationEnabled) return;

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(Math.Max(10, _options.ReconciliationPollSeconds)));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<WalletSalaryService>();
                await service.ReconcilePendingBatchesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wallet salary GET-only reconciliation cycle failed.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
