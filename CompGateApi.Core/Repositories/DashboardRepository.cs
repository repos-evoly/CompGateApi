using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Core.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly CompGateApiDbContext _db;

        private const int TRXCAT_CERT_STMT = 9;   // Certified Statement of Account
        private const int TRXCAT_SALARY = 17;     // Salary Payment (per docs)

        public DashboardRepository(CompGateApiDbContext db)
            => _db = db;

        public async Task<DashboardCommissionSummary> GetCommissionSummaryAsync(DateTime? from, DateTime? to)
        {
            var (start, end) = NormalizeRange(from, to);
            var result = new DashboardCommissionSummary();

            // 1) Statement of Account commissions (service fees): sum TransferRequests.Amount where category = TRXCAT_CERT_STMT
            {
                var q = _db.TransferRequests
                    .Include(t => t.Currency)
                    .Where(t => t.TransactionCategoryId == TRXCAT_CERT_STMT);

                if (start != null) q = q.Where(t => t.RequestedAt >= start);
                if (end != null) q = q.Where(t => t.RequestedAt < end);

                var list = await q.AsNoTracking().ToListAsync();

                decimal lyd = list.Where(t => (t.Currency.Code ?? "").ToUpper() == "LYD").Sum(t => t.Amount);
                decimal other = list.Where(t => (t.Currency.Code ?? "").ToUpper() != "LYD").Sum(t => t.Amount);

                var accounts = list
                    .Select(t => t.ToAccount)
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Distinct()
                    .ToList();

                result.CommissionBoxes.Add(new CommissionBox
                {
                    Type = "statement",
                    AccountNumbers = accounts,
                    LydValue = lyd,
                    OtherCurrenciesValue = other
                });
            }

            // 2) Internal transfers commissions: sum TransferRequests.CommissionAmount where CommissionAmount > 0
            //    Commission receiver accounts are in Settings (LYD/other)
            {
                var q = _db.TransferRequests
                    .Include(t => t.Currency)
                    .Where(t => t.CommissionAmount > 0);

                if (start != null) q = q.Where(t => t.RequestedAt >= start);
                if (end != null) q = q.Where(t => t.RequestedAt < end);

                var list = await q.AsNoTracking().ToListAsync();

                decimal lyd = list.Where(t => (t.Currency.Code ?? "").ToUpper() == "LYD").Sum(t => t.CommissionAmount);
                decimal other = list.Where(t => (t.Currency.Code ?? "").ToUpper() != "LYD").Sum(t => t.CommissionAmount);

                // Commission accounts from Settings
                var settings = await _db.Settings.AsNoTracking().OrderByDescending(s => s.Id).FirstOrDefaultAsync();
                var accs = new List<string>();
                if (settings != null)
                {
                    if (!string.IsNullOrWhiteSpace(settings.CommissionAccount)) accs.Add(settings.CommissionAccount);
                    if (!string.IsNullOrWhiteSpace(settings.CommissionAccountUSD)) accs.Add(settings.CommissionAccountUSD);
                }

                result.CommissionBoxes.Add(new CommissionBox
                {
                    Type = "transfers",
                    AccountNumbers = accs.Distinct().ToList(),
                    LydValue = lyd,
                    OtherCurrenciesValue = other
                });
            }

            // 3) Salaries posting commissions: sum SalaryEntry.CommissionAmount; account is from Pricing.GL1 or derived from debit account
            {
                var qCycles = _db.SalaryCycles
                    .Include(c => c.Entries)
                    .Where(c => c.PostedAt != null);

                if (start != null) qCycles = qCycles.Where(c => c.PostedAt >= start);
                if (end != null) qCycles = qCycles.Where(c => c.PostedAt < end);

                var cycles = await qCycles.AsNoTracking().ToListAsync();

                decimal lyd = 0m, other = 0m;
                var accounts = new HashSet<string>();

                // Pricing for salary to find GL if configured
                var pricing = await _db.Pricings.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.TrxCatId == TRXCAT_SALARY && p.Unit == 1);

                foreach (var c in cycles)
                {
                    var cur = (c.Currency ?? "").Trim().ToUpper();
                    var cycleCommission = c.Entries.Where(e => e.IsTransferred).Sum(e => e.CommissionAmount);
                    if (string.Equals(cur, "LYD", StringComparison.OrdinalIgnoreCase)) lyd += cycleCommission; else other += cycleCommission;

                    var acc = ResolveSalaryCommissionAccount(pricing?.GL1, c.DebitAccount);
                    if (!string.IsNullOrWhiteSpace(acc)) accounts.Add(acc);
                }

                result.CommissionBoxes.Add(new CommissionBox
                {
                    Type = "salaries",
                    AccountNumbers = accounts.ToList(),
                    LydValue = lyd,
                    OtherCurrenciesValue = other
                });
            }

            return result;
        }

        public async Task<DashboardTotalsSummary> GetTotalsAsync(DateTime? from, DateTime? to)
        {
            var (start, end) = NormalizeRange(from, to);

            var totals = new DashboardTotalsSummary();

            // Internal transfers: TransferRequests with CommissionAmount > 0
            {
                var q = _db.TransferRequests.Where(t => t.CommissionAmount > 0);
                if (start != null) q = q.Where(t => t.RequestedAt >= start);
                if (end != null) q = q.Where(t => t.RequestedAt < end);
                totals.InternalTransfers = await q.AsNoTracking().CountAsync();
            }

            // Check requests
            {
                var q = _db.CheckRequests.AsQueryable();
                if (start != null) q = q.Where(x => x.CreatedAt >= start);
                if (end != null) q = q.Where(x => x.CreatedAt < end);
                totals.CheckRequests = await q.AsNoTracking().CountAsync();
            }

            // CheckBook requests
            {
                var q = _db.CheckBookRequests.AsQueryable();
                if (start != null) q = q.Where(x => x.CreatedAt >= start);
                if (end != null) q = q.Where(x => x.CreatedAt < end);
                totals.CheckBookRequests = await q.AsNoTracking().CountAsync();
            }

            // Salaries (cycles created)
            {
                var q = _db.SalaryCycles.Where(x => x.PostedAt != null).AsQueryable();
                if (start != null) q = q.Where(x => x.PostedAt >= start);
                if (end != null) q = q.Where(x => x.PostedAt < end);
                totals.Salaries = await q.AsNoTracking().CountAsync();
            }

            return totals;
        }

        private static (DateTime?, DateTime?) NormalizeRange(DateTime? from, DateTime? to)
        {
            DateTime? start = from?.Date;
            DateTime? end = to?.Date.AddDays(1); // exclusive upper bound
            return (start, end);
        }

        private static string ResolveSalaryCommissionAccount(string? pricingGl1, string debitAccount)
        {
            var src = (debitAccount ?? "").Trim();
            if (src.Length < 13) return string.Empty;
            var branch = src.Substring(0, 4);
            var ccy3 = src.Substring(10, 3);

            if (!string.IsNullOrWhiteSpace(pricingGl1))
            {
                // If GL1 contains {BRANCH}, replace; else use as-is
                if (pricingGl1.Contains("{BRANCH}", StringComparison.OrdinalIgnoreCase))
                    return pricingGl1.Replace("{BRANCH}", branch, StringComparison.OrdinalIgnoreCase);
                return pricingGl1;
            }

            // Fallback: {BRANCH}{932702}{CCY3}
            return $"{branch}932702{ccy3}";
        }
    }
}
