using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CompGateApi.Core.OnePay;

public static partial class OnePayChecksum
{
    public static string Generate(string exactJson, string password, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(exactJson);
        ArgumentNullException.ThrowIfNull(password);

        var counter = now.ToUniversalTime().ToUnixTimeSeconds() / 60;
        var counterHex = counter.ToString("X", CultureInfo.InvariantCulture)
            .PadLeft(16, '0')
            .ToUpperInvariant();

        var otpInput = Encoding.UTF8.GetBytes(password + counterHex);
        var otp = Convert.ToHexString(MD5.HashData(otpInput));

        var checksumText = ArabicCharacters().Replace(exactJson, string.Empty);
        using var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(otp));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.ASCII.GetBytes(checksumText)));
    }

    [GeneratedRegex("[\\u0600-\\u06ff]|[\\u0750-\\u077f]|[\\ufb50-\\ufc3f]|[\\ufe70-\\ufefc]", RegexOptions.Compiled)]
    private static partial Regex ArabicCharacters();
}
