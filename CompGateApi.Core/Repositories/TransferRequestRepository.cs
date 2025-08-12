using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;


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

        // ── COMPANY (“my transfers”) ────────────────────────────────────────

        public async Task<int> GetCountByCompanyAsync(int companyId, string? searchTerm)
        {
            var q = _db.TransferRequests
                       .Where(t => t.CompanyId == companyId);

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

        // ── ADMIN ───────────────────────────────────────────────────────────

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
            var q = _db.TransferRequests
                       .Include(t => t.TransactionCategory);

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

        // ── SINGLE LOOKUP ───────────────────────────────────────────────────

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
                // 1) Determine B2B/B2C via STCOD of receiver
                var stcod = await GetStCodeByAccount(dto.ToAccount);
                if (string.IsNullOrWhiteSpace(stcod))
                    return Fail("Receiver account type unknown");

                bool isB2B = stcod == "CD";
                string transferMode = isB2B ? "B2B" : "B2C";

                // 2) Currency & Settings
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

                // 3) Package detail / per-transaction limits
                var detail = await _db.ServicePackageDetails
                    .Include(d => d.TransactionCategory)
                    .FirstOrDefaultAsync(d =>
                        d.ServicePackageId == servicePackageId &&
                        d.TransactionCategoryId == dto.TransactionCategoryId, ct);

                if (detail == null || !detail.IsEnabledForPackage)
                    return Fail("Internal Transfer not allowed");

                decimal? txnLimit = isB2B ? detail.B2BTransactionLimit : detail.B2CTransactionLimit;
                if (txnLimit.HasValue && amountInBase > txnLimit.Value)
                    return Fail("Transaction limit exceeded");

                // 4) Daily & monthly limits
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

                // 5) Commission
                decimal fixedFee = isB2B ? detail.B2BFixedFee ?? 0 : detail.B2CFixedFee ?? 0;
                decimal pct = isB2B ? detail.B2BCommissionPct ?? 0 : detail.B2CCommissionPct ?? 0;
                decimal pctFee = dto.Amount * (pct / 100m);
                decimal commission = Math.Round(Math.Max(fixedFee, pctFee), 3);

                // 6) Currency code mapping for the bank
                string currencyCode = currency.Id switch
                {
                    1 => "LYD",
                    2 => "USD",
                    3 => "EUR",
                    _ => "LYD"
                };

                // Scale amounts to 3 decimals as fixed-width numeric strings
                const int DECIMALS = 3;
                decimal scale = (decimal)Math.Pow(10, DECIMALS);
                string amountStr = ((long)(dto.Amount * scale)).ToString("D15");
                string commStr = ((long)(commission * scale)).ToString("D15");

                // 7) Company config: who pays commission
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
                var payload = new
                {
                    Header = new
                    {
                        system = "MOBILE",
                        referenceId = Guid.NewGuid().ToString("N")[..16],
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

                var httpClient = _httpFactory.CreateClient();
                var response = await httpClient.PostAsJsonAsync("http://10.3.3.11:7070/api/mobile/flexPostTransfer", payload, ct);
                var bankRaw = await response.Content.ReadAsStringAsync(ct);

                _log.LogInformation("📥 Bank response: {Raw}", bankRaw);

                if (!response.IsSuccessStatusCode)
                    return Fail("Bank error: " + response.StatusCode);

                // Check ReturnCode == "success"
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
                    // If shape unexpected, still fail gracefully
                    return Fail("Bank rejected: invalid response");
                }

                // 9) Persist transfer
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
                    TransferMode = transferMode
                };

                _db.TransferRequests.Add(entity);
                await _db.SaveChangesAsync(ct);

                // 10) Done
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

        // ── EXTERNAL ACCOUNT LOOKUP ─────────────────────────────────────────

        public async Task<List<AccountDto>> GetAccountsAsync(string codeOrAccount)
        {
            string code = codeOrAccount.Length == 6
                ? codeOrAccount
                : codeOrAccount.Length == 13
                    ? codeOrAccount.Substring(4, 6)
                    : throw new ArgumentException("Must be 6 or 13 digits", nameof(codeOrAccount));

            var client = _httpFactory.CreateClient("BankApi");

            var accountsTask = client.PostAsJsonAsync("/api/mobile/accounts", new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = Guid.NewGuid().ToString("N").Substring(0, 16),
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

            var stcodTask = client.PostAsJsonAsync("/api/mobile/GetCustomerInfo", new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = Guid.NewGuid().ToString("N").Substring(0, 16),
                    userName = "TEDMOB",
                    customerNumber = code,
                    requestTime = DateTime.UtcNow.ToString("o"),
                    language = "AR"
                },
                Details = new Dictionary<string, string> {
            { "@CID", code }
        }
            });

            await Task.WhenAll(accountsTask, stcodTask);

            var accountsResp = accountsTask.Result;
            var stcodResp = stcodTask.Result;

            if (!accountsResp.IsSuccessStatusCode) return new();

            var bankDto = await accountsResp.Content.ReadFromJsonAsync<ExternalAccountsResponseDto>();
            if (bankDto?.Details?.Accounts == null) return new();

            string? transferType = null;
            if (stcodResp.IsSuccessStatusCode)
            {
                using var json = JsonDocument.Parse(await stcodResp.Content.ReadAsStringAsync());
                if (json.RootElement.TryGetProperty("Details", out var details) &&
                    details.TryGetProperty("CustInfo", out var custArr) &&
                    custArr.GetArrayLength() > 0)
                {
                    var stcod = custArr[0].GetProperty("STCOD").GetString()?.Trim();
                    transferType = stcod switch
                    {
                        "CD" => "B2B",
                        "EA" => "B2C",
                        _ => null
                    };
                }
            }

            return bankDto.Details.Accounts.Select(a => new AccountDto
            {
                AccountString = $"{a.YBCD01AB}{a.YBCD01AN}{a.YBCD01AS}".Trim(),
                AvailableBalance = a.YBCD01CABL,
                DebitBalance = a.YBCD01LDBL,
                TransferType = transferType
            }).ToList();
        }

        // ── EXTERNAL STATEMENT ───────────────────────────────────────────────

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

                var narrs = new List<string>();
                if (el.TryGetProperty("YBCD04NAR1", out var n1) && n1.GetString() is string s1 && s1.Trim() != "")
                    narrs.Add(s1.Trim());
                if (el.TryGetProperty("YBCD04NAR2", out var n2) && n2.GetString() is string s2 && s2.Trim() != "")
                    narrs.Add(s2.Trim());

                list.Add(new StatementEntryDto
                {
                    PostingDate = DateTime.TryParse(date, out var parsedDate) ? parsedDate : default,
                    DrCr = drcr,
                    Amount = amt,
                    Narratives = narrs
                });
            }

            return list;
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

                    Details = new Dictionary<string, string>
            {
                { "@CID", cid }
            }
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
                {
                    _log.LogError("Missing 'Details' in response for account {Account}. Raw: {Raw}", account, raw);
                    return null;
                }

                if (!details.TryGetProperty("CustInfo", out var custArr) || custArr.ValueKind != JsonValueKind.Array || custArr.GetArrayLength() == 0)
                {
                    _log.LogError("Missing or empty 'CustInfo' for account {Account}. Raw: {Raw}", account, raw);
                    return null;
                }

                var stcod = custArr[0].GetProperty("STCOD").GetString();
                if (string.IsNullOrWhiteSpace(stcod))
                {
                    _log.LogWarning("STCOD is empty or null for account: {Account}", account);
                    return null;
                }

                _log.LogInformation("Fetched STCOD={Stcod} for account {Account}", stcod, account);
                return stcod;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to fetch STCOD for account {Account}", account);
                return null;
            }
        }



        // ── helper classes for external JSON ────────────────────────────────
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
            public decimal YBCD01CABL { get; set; }
            public decimal YBCD01LDBL { get; set; }
        }
    }
}
