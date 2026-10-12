namespace CompGateApi.Core.WalletSalary;

/// <summary>
/// Canonicalizes Libyan mobile numbers for wallet-provider requests.
/// The provider contract uses the E.164 form: +218 followed by the
/// nine-digit national mobile number (without the domestic trunk zero).
/// </summary>
public static class LibyanPhoneNumber
{
    private const string CountryCode = "218";
    private const int NationalNumberLength = 9;

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var compact = RemoveSafeSeparators(value);
        if (compact.Length == 0)
            return false;

        string nationalNumber;
        if (compact.StartsWith("+218", StringComparison.Ordinal))
        {
            nationalNumber = compact[4..];
        }
        else if (compact.StartsWith("00218", StringComparison.Ordinal))
        {
            nationalNumber = compact[5..];
        }
        else if (compact.StartsWith("218", StringComparison.Ordinal))
        {
            nationalNumber = compact[3..];
        }
        else if (compact.StartsWith('0'))
        {
            nationalNumber = compact[1..];
        }
        else
        {
            nationalNumber = compact;
        }

        if (nationalNumber.Length != NationalNumberLength ||
            nationalNumber[0] != '9' ||
            !nationalNumber.All(IsAsciiDigit))
        {
            return false;
        }

        normalized = $"+{CountryCode}{nationalNumber}";
        return true;
    }

    private static string RemoveSafeSeparators(string value)
    {
        var compact = new char[value.Length];
        var length = 0;

        foreach (var character in value.Trim())
        {
            if (char.IsWhiteSpace(character) || character is '-' or '.' or '(' or ')')
                continue;

            compact[length++] = character;
        }

        return new string(compact, 0, length);
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';
}
