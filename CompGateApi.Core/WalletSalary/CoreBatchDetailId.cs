using System.Globalization;

namespace CompGateApi.Core.WalletSalary;

/// <summary>
/// Builds the line identifier used by Core PostBatchApply. Core returns
/// YBCD10DID as a maximum of 16 characters, so the sequence must be included
/// inside that limit rather than appended to an already-long batch reference.
/// </summary>
public static class CoreBatchDetailId
{
    public const int MaxLength = 16;
    private const int SequenceWidth = 3;
    private const int ReferencePrefixLength = MaxLength - SequenceWidth;

    public static string Create(string reference, int sequence)
    {
        var normalizedReference = reference?.Trim() ?? string.Empty;
        if (normalizedReference.Length < ReferencePrefixLength)
        {
            throw new ArgumentException(
                $"Core batch reference must contain at least {ReferencePrefixLength} characters.",
                nameof(reference));
        }

        if (sequence is < 1 or > 999)
            throw new ArgumentOutOfRangeException(nameof(sequence), "Core batch line sequence must be between 1 and 999.");

        return string.Concat(
            normalizedReference.AsSpan(0, ReferencePrefixLength),
            sequence.ToString("D3", CultureInfo.InvariantCulture));
    }
}
