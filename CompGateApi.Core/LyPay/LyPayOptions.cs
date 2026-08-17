namespace CompGateApi.Core.LyPay;

public sealed class LyPayReconciliationOptions
{
    public const string SectionName = "LyPayReconciliation";

    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 15;
    public int RetryDelaySeconds { get; set; } = 60;
    public int MaxStatusChecks { get; set; } = 3;
    public int ClaimLeaseSeconds { get; set; } = 120;
    public int BatchSize { get; set; } = 20;
}
