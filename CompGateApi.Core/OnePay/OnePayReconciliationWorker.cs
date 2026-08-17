using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompGateApi.Core.OnePay;

public sealed class OnePayReconciliationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OnePayReconciliationOptions _options;
    private readonly ILogger<OnePayReconciliationWorker> _logger;

    public OnePayReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OnePayReconciliationOptions> options,
        ILogger<OnePayReconciliationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("OnePay reconciliation worker is disabled");
            return;
        }

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(Math.Max(5, _options.PollIntervalSeconds)));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<OnePayTransferService>();
                await service.ProcessPendingTransfersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled OnePay reconciliation cycle error");
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
