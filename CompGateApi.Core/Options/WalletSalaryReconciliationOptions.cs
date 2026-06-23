namespace CompGateApi.Core.Options;

public class WalletSalaryReconciliationOptions
{
    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 60;
    public int RetryDelaySeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 3;
    public int LockMinutes { get; set; } = 5;
    public string DefaultMode { get; set; } = "payment_retry";
    public Dictionary<string, WalletSalaryChannelReconciliationOptions> Channels { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public WalletSalaryChannelReconciliationOptions ForChannel(string channel)
    {
        return Channels.TryGetValue(channel, out var options)
            ? options
            : new WalletSalaryChannelReconciliationOptions
            {
                ReconciliationMode = DefaultMode,
                MaxAttempts = MaxAttempts,
                RetryDelaySeconds = RetryDelaySeconds
            };
    }
}

public class WalletSalaryChannelReconciliationOptions
{
    public string ReconciliationMode { get; set; } = "payment_retry";
    public int? MaxAttempts { get; set; }
    public int? RetryDelaySeconds { get; set; }
}
