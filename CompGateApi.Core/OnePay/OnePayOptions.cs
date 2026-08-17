namespace CompGateApi.Core.OnePay;

public sealed class OnePayOptions
{
    public const string SectionName = "OnePay";

    public string BaseUrl { get; set; } = "http://100.1.1.252:8321/api/";
    public string SystemId { get; set; } = "CorpGate";
    public string ChecksumPassword { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int ValidationSessionMinutes { get; set; } = 15;
    public int InstitutionCacheMinutes { get; set; } = 30;
}

public sealed class OnePayReconciliationOptions
{
    public const string SectionName = "OnePayReconciliation";

    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 15;
    public int RetryDelaySeconds { get; set; } = 60;
    public int MaxStatusChecks { get; set; } = 3;
    public int ClaimLeaseSeconds { get; set; } = 120;
    public int BatchSize { get; set; } = 20;
}
