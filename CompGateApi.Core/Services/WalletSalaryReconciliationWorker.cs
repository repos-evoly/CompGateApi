using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class WalletSalaryReconciliationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<WalletSalaryReconciliationOptions> _options;
    private readonly ILogger<WalletSalaryReconciliationWorker> _logger;

    public WalletSalaryReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<WalletSalaryReconciliationOptions> options,
        ILogger<WalletSalaryReconciliationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.Value;
            var interval = TimeSpan.FromSeconds(Math.Max(10, options.PollIntervalSeconds));

            if (options.Enabled)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IEmployeeSalaryRepository>();
                    var processed = await repository.ReconcilePendingWalletBatchesAsync(stoppingToken);
                    if (processed > 0)
                        _logger.LogInformation("Processed {Count} wallet salary reconciliation batch(es).", processed);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Wallet salary reconciliation worker failed.");
                }
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
