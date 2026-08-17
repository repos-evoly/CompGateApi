using CompGateApi.Data.Models;

namespace CompGateApi.Core.Beneficiaries;

public static class PaymentBeneficiaryRules
{
    public static string NormalizeAccount(PaymentRail rail, string? account)
    {
        var value = account?.Trim() ?? string.Empty;
        if (rail == PaymentRail.Normal)
            return value;

        value = value.Replace("-", string.Empty).Replace(" ", string.Empty);
        return rail == PaymentRail.LyPay ? value.ToUpperInvariant() : value;
    }

    public static string NormalizeInstitutionId(string? institutionId) =>
        institutionId?.Trim() ?? string.Empty;

    public static string? ValidateDestination(
        PaymentRail rail,
        string institutionId,
        string accountNumber)
    {
        if (rail == PaymentRail.Normal)
            return null;

        if (string.IsNullOrWhiteSpace(institutionId))
            return "Institution is required for this payment rail.";
        if (institutionId.Length > 50)
            return "Institution reference cannot exceed 50 characters.";
        if (string.IsNullOrWhiteSpace(accountNumber))
            return "Account number is required.";
        if (accountNumber.Length > 34)
            return "Account number cannot exceed 34 characters.";

        if (rail == PaymentRail.OnePay)
        {
            return accountNumber.Any(character => !char.IsLetterOrDigit(character))
                ? "OnePay account number can contain only letters and numbers."
                : null;
        }

        if (!IsValidLibyanIban(accountNumber))
            return "LyPay account number must be a valid 25-character Libyan IBAN.";

        return string.Equals(accountNumber.Substring(4, 3), institutionId, StringComparison.Ordinal)
            ? null
            : "The bank code in the LyPay IBAN does not match the selected institution.";
    }

    public static bool IsValidLibyanIban(string iban)
    {
        if (iban.Length != 25 ||
            !iban.StartsWith("LY", StringComparison.Ordinal) ||
            iban.Skip(2).Any(character => character is < '0' or > '9'))
        {
            return false;
        }

        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var character in rearranged)
        {
            if (char.IsDigit(character))
            {
                remainder = (remainder * 10 + character - '0') % 97;
                continue;
            }

            var letterValue = character - 'A' + 10;
            remainder = (remainder * 10 + letterValue / 10) % 97;
            remainder = (remainder * 10 + letterValue % 10) % 97;
        }

        return remainder == 1;
    }
}
