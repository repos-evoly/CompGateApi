using CompGateApi.Data.Models;
using System.Text.RegularExpressions;

namespace CompGateApi.Core.Transfers;

public static class TransferCommissionAccounts
{
    public static string Normalize(string? account) =>
        Regex.Replace(account?.Trim() ?? "", @"[\s-]", "");

    public static bool IsValid(string account) => Regex.IsMatch(account, @"\A[0-9]{13}\z");

    public static bool TryResolve(Settings settings, string currencyCode, decimal commission,
        out string account, out string? error)
    {
        var code = currencyCode.Trim().ToUpperInvariant();
        account = code switch
        {
            "LYD" => settings.CommissionAccount,
            "USD" => settings.CommissionAccountUSD,
            "EUR" => Normalize(settings.CommissionAccountEUR),
            _ => ""
        };
        error = null;
        if (code is not ("LYD" or "USD" or "EUR"))
            error = "Unsupported transfer currency.";
        else if (code == "EUR" && commission > 0 && !IsValid(account))
            error = "A valid EUR commission account must be configured before posting an EUR transfer with a commission.";

        return error is null;
    }
}
