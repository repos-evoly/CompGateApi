using System.Globalization;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompGateApi.Core.Notifications;

public static class NotificationMessageText
{
    public static string TransferDetails(decimal amount, string? currency, string? recipientName, string? account)
    {
        var code = Clean(currency, 3).ToUpperInvariant();
        var formatted = amount.ToString(code == "LYD" ? "#,##0.000#" : "#,##0.00##", CultureInfo.InvariantCulture);
        var recipient = Clean(recipientName, 150);
        if (recipient.Length == 0)
        {
            var destination = Clean(account, 34);
            recipient = destination.Length >= 4 ? $"account ending {destination[^4..]}" : "the destination account";
        }
        return $"{formatted} {(code.Length == 0 ? "(currency unavailable)" : code)} to {recipient}";
    }

    public static string SalaryPeriod(string? salaryMonth, string? additionalMonth)
    {
        var month = Clean(salaryMonth, 40);
        var extra = Clean(additionalMonth, 10);
        return $"{(month.Length == 0 ? "the selected month" : month)}" +
            (extra.Length == 0 ? "" : $" (additional month: {extra})");
    }

    public static async Task<string> TransferDetailsAsync(TransferRequest transfer,
        CompGateApiDbContext db, ITransferRequestRepository repository, ILogger logger, CancellationToken cancellationToken)
    {
        var currency = transfer.Currency?.Code;
        if (string.IsNullOrWhiteSpace(currency))
            currency = await db.Currencies.AsNoTracking().Where(c => c.Id == transfer.CurrencyId)
                .Select(c => c.Code).FirstOrDefaultAsync(cancellationToken);
        var account = transfer.ToAccount.Trim();
        // Resolve only this company's active normal-transfer beneficiary, never another company's label.
        var name = await db.Beneficiaries.AsNoTracking()
            .Where(b => b.CompanyId == transfer.CompanyId && !b.IsDeleted && b.PaymentRail == PaymentRail.Normal &&
                b.AccountNumber.Trim() == account)
            .OrderBy(b => b.Id).Select(b => b.Name).FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(name))
        {
            try
            {
                var accounts = await repository.GetAccountsAsync(account);
                var destination = accounts.FirstOrDefault(a => string.Equals(a.AccountString?.Trim(), account, StringComparison.OrdinalIgnoreCase));
                name = string.IsNullOrWhiteSpace(destination?.CompanyName) ? destination?.AccountName : destination.CompanyName;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                // Optional display enrichment must not fail a transfer when the bank lookup is unavailable.
                logger.LogWarning("Recipient name unavailable for transfer notification {TransferId}; using masked account.", transfer.Id);
            }
        }
        return TransferDetails(transfer.Amount, currency, name, account);
    }

    private static string Clean(string? value, int maximumLength)
    {
        var text = string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= maximumLength ? text : text[..maximumLength];
    }
}
