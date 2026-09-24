using System.Globalization;
using CompGateApi.Core.Notifications;
using Xunit;

namespace CompGateApi.Tests;

public sealed class NotificationMessageTextTests
{
    [Theory]
    [InlineData("LYD", "1,500.125 LYD to Ahmed Ali")]
    [InlineData("usd", "1,500.125 USD to Ahmed Ali")]
    public void TransferIncludesAmountCurrencyAndRecipientWithoutLosingPrecision(string code, string expected) =>
        Assert.Equal(expected, NotificationMessageText.TransferDetails(1500.125m, code, " Ahmed\nAli ", "123456789"));

    [Fact]
    public void AmountUsesStableFormattingAndRetainsFourthDecimal()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal("1,234.5678 LYD to Ahmed", NotificationMessageText.TransferDetails(1234.5678m, "LYD", "Ahmed", null));
            Assert.Equal("1,500.00 USD to Ahmed", NotificationMessageText.TransferDetails(1500m, "USD", "Ahmed", null));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void MissingRecipientUsesMaskedAccountAndDoesNotInventCurrency()
    {
        var message = NotificationMessageText.TransferDetails(100m, null, " ", "0015123456789");
        Assert.Equal("100.00 (currency unavailable) to account ending 6789", message);
        Assert.DoesNotContain("001512345", message);
        Assert.EndsWith("to the destination account", NotificationMessageText.TransferDetails(1m, "USD", null, null));
    }

    [Theory]
    [InlineData("September", null, "September")]
    [InlineData("سبتمبر", "13", "سبتمبر (additional month: 13)")]
    [InlineData("September", "24", "September (additional month: 24)")]
    [InlineData(" September ", "  ", "September")]
    [InlineData(null, null, "the selected month")]
    public void SalaryUsesStoredMonthAndOptionalAdditionalMonthWithoutInventingYear(string? month, string? extra, string expected) =>
        Assert.Equal(expected, NotificationMessageText.SalaryPeriod(month, extra));
}
