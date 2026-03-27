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
                       .Include(t => t.Currency)
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
            IQueryable<TransferRequest> q = _db.TransferRequests
                .Include(t => t.TransactionCategory)
                .Include(t => t.Currency);

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

        public async Task<TransferRequest?> GetByIdAsync(int id)
            => await _db.TransferRequests
                        .Include(t => t.TransactionCategory)
                        .Include(t => t.Currency)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == id);

        // Step 1: Create a draft transfer without posting to core bank
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
            CreateDraftAsync(int userId,
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
                    return (false, "Receiver account type unknown", null, "0.000", "0.000", 0m, 0m, 0m, 0m, 0m, 0m);

                bool isB2B = stcod == "CD";
                string transferMode = isB2B ? "B2B" : "B2C";

                var currency = await _db.Currencies.FindAsync(new object?[] { dto.CurrencyId }, ct);
                if (currency == null)
                    return (false, "Invalid currency", null, "0.000", "0.000", 0m, 0m, 0m, 0m, 0m, 0m);

                string currencyCode = currency.Id switch { 1 => "LYD", 2 => "USD", 3 => "EUR", _ => "LYD" };
                decimal rate = currency.Rate;
                decimal amountInBase = dto.Amount * rate;

                var settings = await _db.Settings.FirstOrDefaultAsync(ct);
                if (settings == null)
                    return (false, "System settings missing", null, "0.000", "0.000", 0m, 0m, 0m, 0m, 0m, 0m);

                if (settings.GlobalLimit > 0 && amountInBase > settings.GlobalLimit)
                    return (false, "Global limit exceeded", null, "0.000", "0.000", 0m, settings.GlobalLimit, 0m, 0m, 0m, 0m);

                var detail = await _db.ServicePackageDetails
                    .Include(d => d.TransactionCategory)
                    .FirstOrDefaultAsync(d => d.ServicePackageId == servicePackageId && d.TransactionCategoryId == dto.TransactionCategoryId, ct);

                if (detail == null || !detail.IsEnabledForPackage)
                    return (false, "Internal Transfer not allowed", null, "0.000", "0.000", 0m, settings.GlobalLimit, 0m, 0m, 0m, 0m);

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
                    return (false, "Service package missing", null, "0.000", "0.000", 0m, settings.GlobalLimit, 0m, 0m, 0m, 0m);

                if (pkg.DailyLimit > 0 && todayTotal + amountInBase > pkg.DailyLimit)
                    return (false, "Daily limit exceeded", null, "0.000", "0.000", 0m, settings.GlobalLimit, pkg.DailyLimit, pkg.MonthlyLimit, todayTotal, monthTotal);
                if (pkg.MonthlyLimit > 0 && monthTotal + amountInBase > pkg.MonthlyLimit)
                    return (false, "Monthly limit exceeded", null, "0.000", "0.000", 0m, settings.GlobalLimit, pkg.DailyLimit, pkg.MonthlyLimit, todayTotal, monthTotal);

                // foreign-currency-aware min commission
                decimal fixedFeeBase = isB2B ? (detail.B2BFixedFee ?? 0) : (detail.B2CFixedFee ?? 0);
                decimal fixedFeeForeign = isB2B ? (detail.B2BFixedFeeForeign ?? fixedFeeBase) : (detail.B2CFixedFeeForeign ?? fixedFeeBase);
                decimal fixedFee = currencyCode == "LYD" ? fixedFeeBase : fixedFeeForeign;
                decimal pct = isB2B ? (detail.B2BCommissionPct ?? 0) : (detail.B2CCommissionPct ?? 0);
                decimal pctFee = dto.Amount * (pct / 100m);
                int commissionDecimals = currencyCode == "LYD" ? 3 : 2;
                decimal commission = Math.Round(Math.Max(fixedFee, pctFee), commissionDecimals);

                int decimals = currencyCode == "LYD" ? 3 : 2;
                var company = await _db.Companies.FindAsync(new object?[] { companyId }, ct);
                if (company == null)
                    return (false, "Company not found", null, "0.000", "0.000", 0m, settings.GlobalLimit, pkg.DailyLimit, pkg.MonthlyLimit, todayTotal, monthTotal);

                bool commissionOnRecipient = company.CommissionOnReceiver;
                string senderTotal = commissionOnRecipient ? dto.Amount.ToString("0.000") : (dto.Amount + commission).ToString("0.000");
                string receiverTotal = commissionOnRecipient ? (dto.Amount - commission).ToString("0.000") : dto.Amount.ToString("0.000");

                // resolve creator display name
                string createdBy = string.Empty;
                var creator = await _db.Users.FindAsync(new object?[] { userId }, ct);
                if (creator != null)
                {
                    var full = ($"{creator.FirstName} {creator.LastName}").Trim();
                    createdBy = string.IsNullOrWhiteSpace(full) ? (creator.Email ?? string.Empty) : full;
                }

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
                    Status = "Pending",
                    EconomicSectorId = dto.EconomicSectorId,
                    CommissionAmount = commission,
                    CommissionOnRecipient = commissionOnRecipient,
                    Rate = rate,
                    TransferMode = transferMode,
                    BankReference = null,
                    CreatedByName = createdBy
                };

                _db.TransferRequests.Add(entity);
                await _db.SaveChangesAsync(ct);

                return (true, null, entity, senderTotal, receiverTotal, commission,
                        settings.GlobalLimit, pkg.DailyLimit, pkg.MonthlyLimit,
                        todayTotal + amountInBase, monthTotal + amountInBase);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unhandled error in CreateDraftAsync()");
                return (false, "Internal error", null, "0.000", "0.000", 0m, 0m, 0m, 0m, 0m, 0m);
            }
        }

        // Step 2: Execute posting to core bank
        public async Task<(bool Success,
                           string? Error,
                           TransferRequest? Entity,
                           string SenderTotal,
                           string ReceiverTotal)>
            ExecuteAsync(int id,
                         int userId,
                         int companyId,
                         string bearer,
                         CancellationToken ct = default)
        {
            try
            {
                var ent = await _db.TransferRequests.FirstOrDefaultAsync(t => t.Id == id, ct);
                if (ent == null)
                    return (false, "Transfer not found", null, "0.000", "0.000");
                if (ent.CompanyId != companyId)
                    return (false, "Not authorized", null, "0.000", "0.000");
                if (!string.Equals(ent.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                    return (false, "Only pending transfers can be posted", null, "0.000", "0.000");

                var currency = await _db.Currencies.FindAsync(new object?[] { ent.CurrencyId }, ct);
                if (currency == null)
                    return (false, "Invalid currency", null, "0.000", "0.000");
                string currencyCode = currency.Id switch { 1 => "LYD", 2 => "USD", 3 => "EUR", _ => "LYD" };
                int decimals = currencyCode == "LYD" ? 3 : 2;
                decimal scale = (decimal)Math.Pow(10, decimals);

                var settings = await _db.Settings.FirstOrDefaultAsync(ct);
                if (settings == null)
                    return (false, "System settings missing", null, "0.000", "0.000");
                string commissionAccount = currencyCode == "USD" ? settings.CommissionAccountUSD : settings.CommissionAccount;

                var amtRounded = Math.Round(ent.Amount, decimals);
                var commRounded = Math.Round(ent.CommissionAmount, decimals);
                string amountStr = ((long)(amtRounded * scale)).ToString("D15");
                string commStr = ((long)(commRounded * scale)).ToString("D15");
                string applyTrn2 = commRounded > 0m ? "Y" : "N";

                var referenceId = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant();
                var payload = new
                {
                    Header = new
                    {
                        system = "CompanyGateway",
                        referenceId = referenceId,
                        userName = "CompanyGateway",
                        customerNumber = ent.ToAccount,
                        requestTime = DateTime.UtcNow.ToString("o"),
                        language = "AR"
                    },
                    Details = new Dictionary<string, string>
                    {
                        ["@TRFCCY"] = currencyCode,
                        ["@SRCACC"] = ent.FromAccount,
                        ["@SRCACC2"] = ent.CommissionOnRecipient ? ent.ToAccount : ent.FromAccount,
                        ["@DSTACC"] = ent.ToAccount,
                        ["@DSTACC2"] = commissionAccount,
                        ["@TRFAMT"] = amountStr,
                        ["@APLYTRN2"] = applyTrn2,
                        ["@TRFAMT2"] = commStr,
                        ["@NR2"] = ent.Description ?? ""
                    }
                };

                var httpClient = _httpFactory.CreateClient("BankApi");
                var response = await httpClient.PostAsJsonAsync("/api/mobile/flexPostTransfer", payload, ct);
                var bankRaw = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                    return (false, "Bank error: " + response.StatusCode, null, "0.000", "0.000");
                try
                {
                    using var doc = JsonDocument.Parse(bankRaw);
                    if (!doc.RootElement.TryGetProperty("Header", out var hdr) ||
                        !hdr.TryGetProperty("ReturnCode", out var rc) ||
                        !string.Equals(rc.GetString(), "success", StringComparison.OrdinalIgnoreCase))
                    {
                        var msg = hdr.TryGetProperty("ReturnMessage", out var rm) ? rm.GetString() : "Unknown";
                        return (false, "Bank rejected: " + msg, null, "0.000", "0.000");
                    }
                }
                catch
                {
                    return (false, "Bank rejected: invalid response", null, "0.000", "0.000");
                }

                // resolve executor display name
                string executedBy = string.Empty;
                var execUser = await _db.Users.FindAsync(new object?[] { userId }, ct);
                if (execUser != null)
                {
                    var full = ($"{execUser.FirstName} {execUser.LastName}").Trim();
                    executedBy = string.IsNullOrWhiteSpace(full) ? (execUser.Email ?? string.Empty) : full;
                }

                ent.Status = "Completed";
                ent.BankReference = referenceId;
                ent.ExecutedByName = executedBy;
                ent.ExecutedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);

                string senderTotal = ent.CommissionOnRecipient ? ent.Amount.ToString("0.000") : (ent.Amount + ent.CommissionAmount).ToString("0.000");
                string receiverTotal = ent.CommissionOnRecipient ? (ent.Amount - ent.CommissionAmount).ToString("0.000") : ent.Amount.ToString("0.000");

                return (true, null, ent, senderTotal, receiverTotal);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unhandled error in ExecuteAsync()");
                return (false, "Internal error", null, "0.000", "0.000");
            }
        }

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

                // Map currency code early to drive precision/rounding
                string currencyCode = currency.Id switch
                {
                    1 => "LYD",
                    2 => "USD",
                    3 => "EUR",
                    _ => "LYD"
                };

                decimal rate = currency.Rate;
                decimal amountInBase = dto.Amount * rate;

                var settings = await _db.Settings.FirstOrDefaultAsync(ct);
                if (settings == null)
                    return Fail("System settings missing");

                // Treat 0 as no global limit
                if (settings.GlobalLimit > 0 && amountInBase > settings.GlobalLimit)
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

                // Treat 0 as no daily/monthly limit
                if (pkg.DailyLimit > 0 && todayTotal + amountInBase > pkg.DailyLimit)
                    return Fail("Daily limit exceeded");
                if (pkg.MonthlyLimit > 0 && monthTotal + amountInBase > pkg.MonthlyLimit)
                    return Fail("Monthly limit exceeded");

                decimal fixedFee = isB2B ? detail.B2BFixedFee ?? 0 : detail.B2CFixedFee ?? 0;
                decimal pct = isB2B ? detail.B2BCommissionPct ?? 0 : detail.B2CCommissionPct ?? 0;
                decimal pctFee = dto.Amount * (pct / 100m);
                // Round commission according to currency precision (LYD=3, USD/EUR=2)
                int commissionDecimals = currencyCode == "LYD" ? 3 : 2;
                decimal commission = Math.Round(Math.Max(fixedFee, pctFee), commissionDecimals);

                // Amount formatting: LYD -> 3 decimals, USD/EUR -> 2 decimals
                int decimals = currencyCode == "LYD" ? 3 : 2;
                decimal scale = (decimal)Math.Pow(10, decimals);
                // Round amounts to the selected precision before scaling
                var amtRounded = Math.Round(dto.Amount, decimals);
                var commRounded = Math.Round(commission, decimals);
                string amountStr = ((long)(amtRounded * scale)).ToString("D15");
                string commStr = ((long)(commRounded * scale)).ToString("D15");
                string applyTrn2 = commRounded > 0m ? "Y" : "N";

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
                        system = "CompanyGateway",
                        referenceId = referenceId,
                        userName = "CompanyGateway",
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
                        ["@APLYTRN2"] = applyTrn2,
                        ["@TRFAMT2"] = commStr,
                        ["@NR2"] = dto.Description ?? ""
                    }
                };

                _log.LogInformation("ðŸ“¤ Bank payload: {Payload}", JsonSerializer.Serialize(payload));

                var httpClient = _httpFactory.CreateClient("BankApi");
                var response = await httpClient.PostAsJsonAsync("/api/mobile/flexPostTransfer", payload, ct);
                var bankRaw = await response.Content.ReadAsStringAsync(ct);

                _log.LogInformation("ðŸ“¥ Bank response: {Raw}", bankRaw);

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
                    BankReference = referenceId // â† NEW
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
                    system = "CompanyGateway",
                    referenceId = header(),
                    userName = "CompanyGateway",
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
                    system = "CompanyGateway",
                    referenceId = header(),
                    userName = "CompanyGateway",
                    customerNumber = code,
                    requestTime = DateTime.UtcNow.ToString("o"),
                    language = "AR"
                },
                Details = new Dictionary<string, string> {
            { "@CID", code }
        }
            });

            // Branches (KYC) â€“ weâ€™ll map CABBN (branch number) -> CABRN (branch name)
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

            // Map accounts â†’ AccountDto (with company name & branch info)
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

                    CompanyName = a.YBCD01CUN?.Trim(), // â† company name from accounts API
                    BranchCode = ab,                  // â† YBCD01AB
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
                    system = "CompanyGateway",
                    referenceId = Guid.NewGuid().ToString("N").Substring(0, 16),
                    userName = "CompanyGateway",
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
                string? trxCode = null;
                if (el.TryGetProperty("YBCD04TCD", out var tcdEl) && tcdEl.ValueKind == JsonValueKind.String)
                {
                    trxCode = tcdEl.GetString()?.Trim();
                }
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
                    Narratives = narrs,
                    TrxCode = trxCode
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
                        system = "CompanyGateway",
                        referenceId = Guid.NewGuid().ToString("N").Substring(0, 16),
                        userName = "CompanyGateway",
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

        // Legacy single-step create-and-post kept for backward compatibility
        private async Task<(bool Success,
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
            CreateAndPostInternalAsync(int userId,
                        int companyId,
                        int servicePackageId,
                        TransferRequestCreateDto dto,
                        string bearer,
                        CancellationToken ct = default)
        {
            var draft = await CreateDraftAsync(userId, companyId, servicePackageId, dto, bearer, ct);
            if (!draft.Success || draft.Entity == null)
                return draft;
            var exec = await ExecuteAsync(draft.Entity.Id, userId, companyId, bearer, ct);
            if (!exec.Success)
                return (false, exec.Error, null, "0.000", "0.000", 0m, 0m, 0m, 0m, 0m, 0m);
            return (true, null, draft.Entity, draft.SenderTotal, draft.ReceiverTotal, draft.Commission,
                    draft.GlobalLimit, draft.DailyLimit, draft.MonthlyLimit, draft.UsedToday, draft.UsedThisMonth);
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

