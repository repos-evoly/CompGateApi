using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompGateApi.Core.LyPay;

public sealed class LyPayReconciliationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LyPayReconciliationOptions _options;
    private readonly ILogger<LyPayReconciliationWorker> _logger;

    public LyPayReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<LyPayReconciliationOptions> options,
        ILogger<LyPayReconciliationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("LyPay reconciliation worker is disabled");
            return;
        }

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(Math.Max(5, _options.PollIntervalSeconds)));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<LyPayTransferService>();
                await service.ProcessPendingTransfersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled LyPay reconciliation cycle error");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
