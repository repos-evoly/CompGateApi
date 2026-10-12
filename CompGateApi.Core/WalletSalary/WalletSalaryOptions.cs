namespace CompGateApi.Core.WalletSalary;

public sealed class WalletSalaryOptions
{
    public const string SectionName = "WalletSalary";

    public string Version { get; set; } = "v2";
    public int TimeoutSeconds { get; set; } = 30;
    public int ReconciliationPollSeconds { get; set; } = 60;
    public int ReconciliationDelaySeconds { get; set; } = 60;
    public int ReconciliationBatchSize { get; set; } = 20;
    public int ReconciliationClaimSeconds { get; set; } = 120;
    public bool ReconciliationEnabled { get; set; } = true;
    public string CommissionMode { get; set; } = "receiverPaid";

    public WalletSalaryRailOptions Evo { get; set; } = new()
    {
        Enabled = true,
        BaseUrl = "https://uatevobackend.evoapps-ly.com"
    };

    public WalletSalaryRailOptions Bcd { get; set; } = new()
    {
        Enabled = false
    };

    public WalletSalaryRailOptions ForRail(string rail) =>
        string.Equals(rail, WalletSalaryChannels.EvoWallet, StringComparison.OrdinalIgnoreCase)
            ? Evo
            : string.Equals(rail, WalletSalaryChannels.BcdWallet, StringComparison.OrdinalIgnoreCase)
                ? Bcd
                : new WalletSalaryRailOptions();
}

public sealed class WalletSalaryRailOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ShadowAccount { get; set; } = string.Empty;

    public bool IsConfigured =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        ShadowAccount.Trim().Length == 13 &&
        ShadowAccount.Trim().All(char.IsDigit);
}

public static class WalletSalaryChannels
{
    public const string BankAccount = "bankAccount";
    public const string EvoWallet = "evoWallet";
    public const string BcdWallet = "bcdWallet";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        new[] { BankAccount, EvoWallet, BcdWallet },
        StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlySet<string> Wallets = new HashSet<string>(
        new[] { EvoWallet, BcdWallet },
        StringComparer.OrdinalIgnoreCase);

    public static string? Normalize(string? value)
    {
        if (string.Equals(value, BankAccount, StringComparison.OrdinalIgnoreCase)) return BankAccount;
        if (string.Equals(value, EvoWallet, StringComparison.OrdinalIgnoreCase)) return EvoWallet;
        if (string.Equals(value, BcdWallet, StringComparison.OrdinalIgnoreCase)) return BcdWallet;
        return null;
    }
}

public static class WalletSalaryRecipientTypes
{
    public const string AccountNumber = "accountNumber";
    public const string EvoId = "evoId";
    public const string PhoneNumber = "phoneNumber";

    public static string? NormalizeWalletType(string? value)
    {
        if (string.Equals(value, EvoId, StringComparison.OrdinalIgnoreCase)) return EvoId;
        if (string.Equals(value, PhoneNumber, StringComparison.OrdinalIgnoreCase)) return PhoneNumber;
        return null;
    }
}
