using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CompGateApi.Data.Repositories
{
    public class TransferRequestRepository : ITransferRequestRepository
    {
        private readonly CompGateApiDbContext _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<TransferRequestRepository> _log;

        public TransferRequestRepository(
            CompGateApiDbContext db,
            IHttpClientFactory httpFactory,
            ILogger<TransferRequestRepository> log)
        {
            _db = db;
            _httpFactory = httpFactory;
            _log = log;
        }

        public async Task<int> GetCountByCompanyAsync(int companyId, string? searchTerm)
        {
            var q = _db.TransferRequests.Where(t => t.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
                q = q.Where(t =>
                    t.FromAccount.Contains(searchTerm!) ||
                    t.ToAccount.Contains(searchTerm!) ||
                    t.Status.Contains(searchTerm!));

            return await q.CountAsync();
        }

        public async Task<List<TransferRequest>> GetAllByCompanyAsync(
            int companyId, string? searchTerm, int page, int limit)
        {
            var q = _db.TransferRequests
                       .Include(t => t.TransactionCategory)
                       .Where(t => t.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
                q = q.Where(t =>
                    t.FromAccount.Contains(searchTerm!) ||
                    t.ToAccount.Contains(searchTerm!) ||
                    t.Status.Contains(searchTerm!));

            return await q
                .OrderByDescending(t => t.RequestedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetCountAsync(string? searchTerm)
        {
            var q = _db.TransferRequests.AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
                q = q.Where(t =>
                    t.FromAccount.Contains(searchTerm!) ||
                    t.ToAccount.Contains(searchTerm!) ||
                    t.Status.Contains(searchTerm!));

            return await q.CountAsync();
        }

        public async Task<List<TransferRequest>> GetAllAsync(
            string? searchTerm, int page, int limit)
        {
            var q = _db.TransferRequests.Include(t => t.TransactionCategory);

            if (!string.IsNullOrWhiteSpace(searchTerm))
                q = (Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<TransferRequest, TransactionCategory>)q.Where(t =>
                    t.FromAccount.Contains(searchTerm!) ||
                    t.ToAccount.Contains(searchTerm!) ||
                    t.Status.Contains(searchTerm!));

            return await q
                .OrderByDescending(t => t.RequestedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<TransferRequest?> GetByIdAsync(int id)
            => await _db.TransferRequests
                        .Include(t => t.TransactionCategory)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == id);

        public async Task<(bool Success,
                           string? Error,
                           TransferRequest? Entity,
                           string SenderTotal,
                           string ReceiverTotal,
                           decimal Commission,
                           decimal GlobalLimit,
                           decimal DailyLimit,
                           decimal MonthlyLimit,
                           decimal UsedToday,
                           decimal UsedThisMonth)>
            CreateAsync(int userId,
                        int companyId,
                        int servicePackageId,
                        TransferRequestCreateDto dto,
                        string bearer,
                        CancellationToken ct = default)
        {
            try
            {
                var stcod = await GetStCodeByAccount(dto.ToAccount);
                if (string.IsNullOrWhiteSpace(stcod))
                    return Fail("Receiver account type unknown");

                bool isB2B = stcod == "CD";
                string transferMode = isB2B ? "B2B" : "B2C";

                var currency = await _db.Currencies.FindAsync(new object?[] { dto.CurrencyId }, ct);
                if (currency == null)
                    return Fail("Invalid currency");

                decimal rate = currency.Rate;
                decimal amountInBase = dto.Amount * rate;

                var settings = await _db.Settings.FirstOrDefaultAsync(ct);
                if (settings == null)
                    return Fail("System settings missing");

                if (amountInBase > settings.GlobalLimit)
                    return Fail("Global limit exceeded");

                var detail = await _db.ServicePackageDetails
                    .Include(d => d.TransactionCategory)
                    .FirstOrDefaultAsync(d =>
                        d.ServicePackageId == servicePackageId &&
                        d.TransactionCategoryId == dto.TransactionCategoryId, ct);

                if (detail == null || !detail.IsEnabledForPackage)
                    return Fail("Internal Transfer not allowed");

                // decimal? txnLimit = isB2B ? detail.B2BTransactionLimit : detail.B2CTransactionLimit;
                // if (txnLimit.HasValue && amountInBase > txnLimit.Value)
                //     return Fail("Transaction limit exceeded");

                var today = DateTime.UtcNow.Date;
                var monthStart = new DateTime(today.Year, today.Month, 1);

                var todayTotal = await _db.TransferRequests
                    .Where(t => t.CompanyId == companyId && t.RequestedAt.Date == today)
                    .Select(t => t.Amount * t.Rate)
                    .SumAsync(ct);

                var monthTotal = await _db.TransferRequests
                    .Where(t => t.CompanyId == companyId && t.RequestedAt >= monthStart)
                    .Select(t => t.Amount * t.Rate)
                    .SumAsync(ct);

                var pkg = await _db.ServicePackages.FindAsync(new object?[] { servicePackageId }, ct);
                if (pkg is null)
                    return Fail("Service package missing");

                if (todayTotal + amountInBase > pkg.DailyLimit)
                    return Fail("Daily limit exceeded");
                if (monthTotal + amountInBase > pkg.MonthlyLimit)
                    return Fail("Monthly limit exceeded");

                decimal fixedFee = isB2B ? detail.B2BFixedFee ?? 0 : detail.B2CFixedFee ?? 0;
                decimal pct = isB2B ? detail.B2BCommissionPct ?? 0 : detail.B2CCommissionPct ?? 0;
                decimal pctFee = dto.Amount * (pct / 100m);
                decimal commission = Math.Round(Math.Max(fixedFee, pctFee), 3);

                string currencyCode = currency.Id switch
                {
                    1 => "LYD",
                    2 => "USD",
                    3 => "EUR",
                    _ => "LYD"
                };

                const int DECIMALS = 3;
                decimal scale = (decimal)Math.Pow(10, DECIMALS);
                string amountStr = ((long)(dto.Amount * scale)).ToString("D15");
                string commStr = ((long)(commission * scale)).ToString("D15");

                var company = await _db.Companies.FindAsync(new object?[] { companyId }, ct);
                if (company == null)
                    return Fail("Company not found");

                bool commissionOnRecipient = company.CommissionOnReceiver;

                string senderTotal = commissionOnRecipient
                            ? dto.Amount.ToString("0.000")
                            : (dto.Amount + commission).ToString("0.000");

                string receiverTotal = commissionOnRecipient
                            ? (dto.Amount - commission).ToString("0.000")
                            : dto.Amount.ToString("0.000");

                string commissionAccount = currencyCode == "USD"
                    ? settings.CommissionAccountUSD
                    : settings.CommissionAccount;

                // 8) Call bank API
                var referenceId = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant();

                var payload = new
                {
                    Header = new
                    {
                        system = "MOBILE",
                        referenceId = referenceId,
                        userName = "TEDMOB",
                        customerNumber = dto.ToAccount,
                        requestTime = DateTime.UtcNow.ToString("o"),
                        language = "AR"
                    },
                    Details = new Dictionary<string, string>
                    {
                        ["@TRFCCY"] = currencyCode,
                        ["@SRCACC"] = dto.FromAccount,
                        ["@SRCACC2"] = commissionOnRecipient ? dto.ToAccount : dto.FromAccount,
                        ["@DSTACC"] = dto.ToAccount,
                        ["@DSTACC2"] = commissionAccount,
                        ["@TRFAMT"] = amountStr,
                        ["@APLYTRN2"] = "Y",
                        ["@TRFAMT2"] = commStr,
                        ["@NR2"] = dto.Description ?? ""
                    }
                };

                _log.LogInformation("📤 Bank payload: {Payload}", JsonSerializer.Serialize(payload));

                var httpClient = _httpFactory.CreateClient("BankApi");
                var response = await httpClient.PostAsJsonAsync("/api/mobile/flexPostTransfer", payload, ct);
                var bankRaw = await response.Content.ReadAsStringAsync(ct);

                _log.LogInformation("📥 Bank response: {Raw}", bankRaw);

                if (!response.IsSuccessStatusCode)
                    return Fail("Bank error: " + response.StatusCode);

                try
                {
                    using var doc = JsonDocument.Parse(bankRaw);
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

                // 9) Persist transfer (store referenceId)
                var entity = new TransferRequest
                {
                    UserId = userId,
                    CompanyId = companyId,
                    TransactionCategoryId = dto.TransactionCategoryId,
                    FromAccount = dto.FromAccount,
                    ToAccount = dto.ToAccount,
                    Amount = dto.Amount,
                    CurrencyId = dto.CurrencyId,
                    ServicePackageId = servicePackageId,
                    Description = dto.Description,
                    RequestedAt = DateTime.UtcNow,
                    Status = "Completed",
                    EconomicSectorId = dto.EconomicSectorId,
                    CommissionAmount = commission,
                    CommissionOnRecipient = commissionOnRecipient,
                    Rate = rate,
                    TransferMode = transferMode,
                    BankReference = referenceId // ← NEW
                };

                _db.TransferRequests.Add(entity);
                await _db.SaveChangesAsync(ct);

                return (true, null, entity, senderTotal, receiverTotal, commission,
                        settings.GlobalLimit, pkg.DailyLimit, pkg.MonthlyLimit,
                        todayTotal + amountInBase, monthTotal + amountInBase);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unhandled error in CreateAsync()");
                return Fail("Internal error");
            }

            (bool, string?, TransferRequest?, string, string, decimal, decimal, decimal, decimal, decimal, decimal)
                Fail(string msg)
                => (false, msg, null, "0.000", "0.000", 0m, 0m, 0m, 0m, 0m, 0m);
        }

        public async Task UpdateAsync(TransferRequest tr)
        {
            _db.TransferRequests.Update(tr);
            await _db.SaveChangesAsync();
        }

        public async Task<List<AccountDto>> GetAccountsAsync(string codeOrAccount)
        {
            // Derive 6-digit customer code from either 6 or 13 digit input
            string code = codeOrAccount.Length == 6
                ? codeOrAccount
                : codeOrAccount.Length == 13
                    ? codeOrAccount.Substring(4, 6)
                    : throw new ArgumentException("Must be 6 or 13 digits", nameof(codeOrAccount));

            var bankClient = _httpFactory.CreateClient("BankApi");
            var kycClient = _httpFactory.CreateClient("KycApi");

            // Prepare payloads
            var header = new Func<string>(() => Guid.NewGuid().ToString("N").Substring(0, 16));

            var accountsTask = bankClient.PostAsJsonAsync("/api/mobile/accounts", new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = header(),
                    userName = "TEDMOB",
                    customerNumber = code,
                    requestTime = DateTime.UtcNow.ToString("o"),
                    language = "AR"
                },
                Details = new Dictionary<string, string> {
            { "@CID", code },
            { "@GETAVB","Y" }
        }
            });

            var stcodTask = bankClient.PostAsJsonAsync("/api/mobile/GetCustomerInfo", new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = header(),
                    userName = "TEDMOB",
                    customerNumber = code,
                    requestTime = DateTime.UtcNow.ToString("o"),
                    language = "AR"
                },
                Details = new Dictionary<string, string> {
            { "@CID", code }
        }
            });

            // Branches (KYC) – we’ll map CABBN (branch number) -> CABRN (branch name)
            var branchesTask = kycClient.GetAsync("kycapi/api/core/getActiveBranches");

            await Task.WhenAll(accountsTask, stcodTask, branchesTask);

            var accountsResp = accountsTask.Result;
            var stcodResp = stcodTask.Result;
            var branchesResp = branchesTask.Result;

            if (!accountsResp.IsSuccessStatusCode) return new();

            var bankDto = await accountsResp.Content.ReadFromJsonAsync<ExternalAccountsResponseDto>();
            if (bankDto?.Details?.Accounts == null) return new();

            // Compute transfer type from STCOD, if available
            string? transferType = null;
            if (stcodResp.IsSuccessStatusCode)
            {
                try
                {
                    using var json = await System.Text.Json.JsonDocument.ParseAsync(await stcodResp.Content.ReadAsStreamAsync());
                    if (json.RootElement.TryGetProperty("Details", out var details) &&
                        details.TryGetProperty("CustInfo", out var custArr) &&
                        custArr.ValueKind == System.Text.Json.JsonValueKind.Array &&
                        custArr.GetArrayLength() > 0 &&
                        custArr[0].TryGetProperty("STCOD", out var stcodEl))
                    {
                        var st = stcodEl.GetString()?.Trim();
                        transferType = st switch
                        {
                            "CD" => "B2B",
                            "EA" => "B2C",
                            _ => null
                        };
                    }
                }
                catch { /* ignore STCOD parsing errors */ }
            }

            // Build a branch map from KYC: CABBN (branch number/code) -> CABRN (branch name)
            var branchMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (branchesResp.IsSuccessStatusCode)
            {
                try
                {
                    using var jdoc = await System.Text.Json.JsonDocument.ParseAsync(await branchesResp.Content.ReadAsStreamAsync());
                    if (jdoc.RootElement.TryGetProperty("Details", out var det) &&
                        det.TryGetProperty("Branches", out var arr) &&
                        arr.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var item in arr.EnumerateArray())
                        {
                            var codeEl = item.TryGetProperty("CABBN", out var c) ? c.GetString() : null; // branch number
                            var nameEl = item.TryGetProperty("CABRN", out var n) ? n.GetString() : null; // branch name
                            if (!string.IsNullOrWhiteSpace(codeEl))
                                branchMap[codeEl!.Trim()] = (nameEl ?? string.Empty).Trim();
                        }
                    }
                }
                catch { /* ignore branches parsing errors; we'll just omit branch names */ }
            }

            // Map accounts → AccountDto (with company name & branch info)
            var result = bankDto.Details.Accounts.Select(a =>
            {
                var ab = a.YBCD01AB?.Trim(); // branch number from accounts API (e.g., "0015")
                branchMap.TryGetValue(ab ?? string.Empty, out var branchName);

                return new AccountDto
                {
                    AccountString = $"{a.YBCD01AB}{a.YBCD01AN}{a.YBCD01AS}".Trim(),
                    AccountName = a.YBCD01SHNA?.Trim(),
                    Currency = a.YBCD01CCY?.Trim(),
                    AvailableBalance = a.YBCD01CABL,
                    DebitBalance = a.YBCD01LDBL,
                    TransferType = transferType,

                    CompanyName = a.YBCD01CUN?.Trim(), // ← company name from accounts API
                    BranchCode = ab,                  // ← YBCD01AB
                    BranchName = string.IsNullOrWhiteSpace(branchName) ? null : branchName
                };
            }).ToList();

            return result;
        }


        public async Task<List<StatementEntryDto>> GetStatementAsync(string account, DateTime from, DateTime to)
        {
            var client = _httpFactory.CreateClient("BankApi");
            var payload = new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = Guid.NewGuid().ToString("N").Substring(0, 16),
                    userName = "TEDMOB",
                    customerNumber = account,
                    requestTime = DateTime.UtcNow.ToString("o"),
                    language = "AR"
                },
                Details = new Dictionary<string, string> {
                    { "@ACC",   account },
                    { "@BYDTE", "Y" },
                    { "@FDATE", from.ToString("yyyyMMdd") },
                    { "@TDATE", to.ToString("yyyyMMdd") },
                    { "@BYNBR", "N" },
                    { "@NBR",   "0" }
                }
            };

            var resp = await client.PostAsJsonAsync("/api/mobile/transactions", payload);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("Details", out var d) ||
                d.ValueKind != JsonValueKind.Object ||
                !d.TryGetProperty("Transactions", out var txns) ||
                txns.ValueKind != JsonValueKind.Array)
                return new();

            var list = new List<StatementEntryDto>();
            foreach (var el in txns.EnumerateArray())
            {
                var date = el.GetProperty("YBCD04POD").GetString()?.Trim() ?? "";
                var drcr = el.GetProperty("YBCD04DRCR").GetString() ?? "";
                var amt = el.GetProperty("YBCD04AMA").GetDecimal();
                decimal bal = 0m;
                if (el.TryGetProperty("YBCD04BAL", out var balEl) && balEl.ValueKind == JsonValueKind.Number)
                {
                    bal = balEl.GetDecimal();
                }

                var narrs = new List<string>();
                if (el.TryGetProperty("YBCD04NAR1", out var n1) && n1.GetString() is string s1 && s1.Trim() != "")
                    narrs.Add(s1.Trim());
                if (el.TryGetProperty("YBCD04NAR2", out var n2) && n2.GetString() is string s2 && s2.Trim() != "")
                    narrs.Add(s2.Trim());

                list.Add(new StatementEntryDto
                {
                    // Format posting date as yyyy-MM-dd for API consumers
                    PostingDate = TryFormatDate(date),
                    DrCr = drcr,
                    Amount = amt,
                    Balance = bal,
                    Narratives = narrs
                });
            }

            return list;
        }

        private static string TryFormatDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            // Try known bank format yyyyMMdd
            if (DateTime.TryParseExact(raw.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out var d1))
            {
                return d1.ToString("yyyy-MM-dd");
            }

            // Fallback: try general parse
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d2))
            {
                return d2.ToString("yyyy-MM-dd");
            }

            return raw.Trim();
        }

        public async Task<string?> GetStCodeByAccount(string account)
        {
            try
            {
                var client = _httpFactory.CreateClient("BankApi");
                var cid = account.Substring(4, 6);

                var payload = new
                {
                    Header = new
                    {
                        system = "MOBILE",
                        referenceId = Guid.NewGuid().ToString("N").Substring(0, 16),
                        userName = "TEDMOB",
                        customerNumber = account,
                        requestTime = DateTime.UtcNow.ToString("o"),
                        language = "AR"
                    },
                    Details = new Dictionary<string, string> { { "@CID", cid } }
                };

                var response = await client.PostAsJsonAsync("/api/mobile/GetCustomerInfo", payload);
                var raw = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _log.LogError("GetCustomerInfo failed for account {Account} with status {Status}", account, response.StatusCode);
                    return null;
                }

                using var doc = JsonDocument.Parse(raw);
                if (!doc.RootElement.TryGetProperty("Details", out var details))
                    return null;

                if (!details.TryGetProperty("CustInfo", out var custArr) || custArr.ValueKind != JsonValueKind.Array || custArr.GetArrayLength() == 0)
                    return null;

                var stcod = custArr[0].GetProperty("STCOD").GetString();
                if (string.IsNullOrWhiteSpace(stcod))
                    return null;

                _log.LogInformation("Fetched STCOD={Stcod} for account {Account}", stcod, account);
                return stcod;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to fetch STCOD for account {Account}", account);
                return null;
            }
        }

        private class ExternalAccountsResponseDto
        {
            public object Header { get; set; } = null!;
            public ExternalAccountsResponseDetailsDto Details { get; set; } = new();
        }
        private class ExternalAccountsResponseDetailsDto
        {
            public List<ExternalAccountDto> Accounts { get; set; } = new();
        }
        private class ExternalAccountDto
        {
            public string? YBCD01AB { get; set; }
            public string? YBCD01AN { get; set; }
            public string? YBCD01AS { get; set; }
            public string? YBCD01SHNA { get; set; }
            public string? YBCD01CCY { get; set; }
            public decimal YBCD01CABL { get; set; }
            public decimal YBCD01LDBL { get; set; }
            public string? YBCD01CUN { get; set; }
        }
    }
}
