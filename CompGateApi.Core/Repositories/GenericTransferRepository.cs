using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompGateApi.Data.Repositories
{
    /// <summary>
    /// Generic, low-friction transfer caller for service fees (e.g., checkbooks).
    /// - Debit: POST /api/mobile/CompanyGatewayPostTransfer
    /// - Refund: POST /api/mobile/flexReverseTransfer
    /// - Supports DTCD/CTCD from Pricing, optional second leg, NR2 override
    /// - Persists a minimal TransferRequest for audit/reversal
    /// </summary>
    public class GenericTransferRepository : IGenericTransferRepository
    {
        private readonly CompGateApiDbContext _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<GenericTransferRepository> _log;

        public GenericTransferRepository(
            CompGateApiDbContext db,
            IHttpClientFactory httpFactory,
            ILogger<GenericTransferRepository> log)
        {
            _db = db;
            _httpFactory = httpFactory;
            _log = log;
        }

        public async Task<(bool Success, string? Error, TransferRequest? Entity, string? BankReference)>
            DebitForServiceAsync(
                int userId,
                int companyId,
                int servicePackageId,
                int trxCategoryId,
                string fromAccount,
                string toAccount,
                decimal amount,
                string description,
                string currencyCode,
                string? dtc,
                string? ctc,
                string? dtc2,
                string? ctc2,
                bool applySecondLeg,
                string? narrativeOverride,
                CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fromAccount) || fromAccount.Length < 10)
                    return Fail("Invalid source account");
                if (string.IsNullOrWhiteSpace(toAccount) || toAccount.Length < 10)
                    return Fail("Invalid destination account");
                if (amount <= 0m)
                    return Fail("Invalid amount");
                if (string.IsNullOrWhiteSpace(currencyCode))
                    currencyCode = "LYD";

                // Amount formatting: 15 digits, 3 decimals
                const int DECIMALS = 3;
                var scale = (decimal)Math.Pow(10, DECIMALS);
                string amountStr = ((long)(amount * scale)).ToString("D15");

                // Banking reference we generate and also persist
                var referenceId = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant();

                // Second leg config
                var applyTrn2 = applySecondLeg ? "Y" : "N";
                var trfAmt2 = "000000000000000"; // no commission handled here

                // Narrative
                var nr2 = string.IsNullOrWhiteSpace(narrativeOverride) ? (description ?? "") : narrativeOverride!;

                var payload = new
                {
                    Header = new
                    {
                        system = "MOBILE",
                        referenceId = referenceId,
                        userName = "TEDMOB",
                        customerNumber = (fromAccount.Length >= 13 ? fromAccount.Substring(4, 6) : ""),
                        requestTime = DateTime.UtcNow.ToString("o"),
                        language = "AR"
                    },
                    Details = new Dictionary<string, string>
                    {
                        ["@TRFCCY"] = currencyCode,
                        ["@SRCACC"] = fromAccount,
                        ["@DSTACC"] = toAccount,
                        // second leg accounts still required by core, even if APLYTRN2="N"
                        ["@SRCACC2"] = fromAccount,
                        ["@DSTACC2"] = toAccount,
                        ["@TRFAMT"] = amountStr,
                        ["@APLYTRN2"] = applyTrn2,
                        ["@TRFAMT2"] = trfAmt2,
                        ["@DTCD"] = dtc ?? "",
                        ["@DTCD2"] = dtc2 ?? (dtc ?? ""),
                        ["@CTCD"] = ctc ?? "",
                        ["@CTCD2"] = ctc2 ?? (ctc ?? ""),
                        ["@NR2"] = nr2
                    }
                };

                _log.LogInformation("📤 CompanyGatewayPostTransfer payload: {Payload}",
                    JsonSerializer.Serialize(payload));

                var httpClient = _httpFactory.CreateClient();
                var resp = await httpClient.PostAsJsonAsync("http://10.3.3.11:7070/api/mobile/postTransfer", payload, ct);
                var raw = await resp.Content.ReadAsStringAsync(ct);

                _log.LogInformation("📥 CompanyGatewayPostTransfer response: {Raw}", raw);

                if (!resp.IsSuccessStatusCode)
                    return Fail("Bank error: " + resp.StatusCode);

                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    if (!doc.RootElement.TryGetProperty("Header", out var hdr) ||
                        !hdr.TryGetProperty("ReturnCode", out var rc) ||
                        !string.Equals(rc.GetString(), "success", StringComparison.OrdinalIgnoreCase))
                    {
                        var msg = hdr.TryGetProperty("ReturnMessage", out var rm) ? rm.GetString() : "Unknown";
                        return Fail("Bank rejected: " + msg);
                    }
                }
                catch
                {
                    return Fail("Bank rejected: invalid response");
                }

                // Persist minimal transfer for audit/reversal
                var tr = new TransferRequest
                {
                    UserId = userId,
                    CompanyId = companyId,
                    TransactionCategoryId = trxCategoryId,
                    FromAccount = fromAccount,
                    ToAccount = toAccount,
                    Amount = amount,
                    CurrencyId = currencyCode == "USD" ? 2 : currencyCode == "EUR" ? 3 : 1, // 1=LYD fallback
                    ServicePackageId = servicePackageId,
                    Description = description,
                    RequestedAt = DateTime.UtcNow,
                    Status = "Completed",
                    EconomicSectorId = null,
                    CommissionAmount = 0m,
                    CommissionOnRecipient = false,
                    Rate = 1m,
                    TransferMode = "B2B",
                    BankReference = referenceId
                };

                _db.TransferRequests.Add(tr);
                await _db.SaveChangesAsync(ct);

                return (true, null, tr, referenceId);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unhandled error in GenericTransferRepository.DebitForServiceAsync");
                return Fail("Internal error");
            }

            static (bool, string?, TransferRequest?, string?) Fail(string msg)
                => (false, msg, null, null);
        }

        public async Task<(bool Success, string? Error)>
            RefundByOriginalRefAsync(
                string originalBankRef,
                string currencyCode,
                string srcAcc,
                string dstAcc,
                string srcAcc2,
                string dstAcc2,
                decimal amount,
                string note,
                CancellationToken ct = default)
        {
            const int DECIMALS = 3;
            decimal scale = (decimal)Math.Pow(10, DECIMALS);
            string amountStr = ((long)(amount * scale)).ToString("D15");

            var payload = new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant(),
                    userName = "TEDMOB",
                    customerNumber = srcAcc?.Length >= 13 ? srcAcc.Substring(4, 6) : "",
                    requestTime = DateTime.UtcNow.ToString("o"),
                    language = "AR"
                },
                Details = new Dictionary<string, string>
                {
                    ["@TRFREFORG"] = originalBankRef,
                    ["@TRFCCY"] = string.IsNullOrWhiteSpace(currencyCode) ? "LYD" : currencyCode,
                    ["@SRCACC"] = srcAcc,   // original DST (fees) → now debited
                    ["@DSTACC"] = dstAcc,   // original SRC (customer) → now credited
                    ["@SRCACC2"] = srcAcc,
                    ["@DSTACC2"] = dstAcc,
                    ["@TRFAMT"] = amountStr,
                    ["@APLYTRN2"] = "N",
                    ["@TRFAMT2"] = "000000000000000",
                    ["@NR2"] = string.IsNullOrWhiteSpace(note) ? "Reversal transaction" : note
                }
            };

            var http = _httpFactory.CreateClient();
            var resp = await http.PostAsJsonAsync("http://10.3.3.11:7070/api/mobile/postTransfer", payload, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);

            _log.LogInformation("🔄 Reverse payload: {Payload}", JsonSerializer.Serialize(payload));
            _log.LogInformation("🔄 Reverse response: {Raw}", raw);

            if (!resp.IsSuccessStatusCode)
                return (false, $"Bank error: {resp.StatusCode}");

            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (!doc.RootElement.TryGetProperty("Header", out var hdr) ||
                    !hdr.TryGetProperty("ReturnCode", out var rc) ||
                    !string.Equals(rc.GetString(), "success", StringComparison.OrdinalIgnoreCase))
                {
                    var msg = hdr.TryGetProperty("ReturnMessage", out var rm) ? rm.GetString() : "Unknown";
                    return (false, "Bank rejected: " + msg);
                }
            }
            catch
            {
                return (false, "Bank rejected: invalid response");
            }

            return (true, null);
        }
    }
}
