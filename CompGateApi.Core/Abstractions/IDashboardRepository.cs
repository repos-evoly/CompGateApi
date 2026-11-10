using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CompGateApi.Core.Abstractions
{
    public interface IDashboardRepository
    {
        Task<DashboardCommissionSummary> GetCommissionSummaryAsync(DateTime? from, DateTime? to);
        Task<DashboardTotalsSummary> GetTotalsAsync(DateTime? from, DateTime? to);
    }

    public class DashboardCommissionSummary
    {
        public List<CommissionBox> CommissionBoxes { get; set; } = new();
    }

    public class CommissionBox
    {
        public string Type { get; set; } = string.Empty; // "statement", "transfers", "salaries"
        public List<string> AccountNumbers { get; set; } = new();
        public decimal LydValue { get; set; }
        public decimal OtherCurrenciesValue { get; set; }
        // Explicit currency/account pairs for new clients; legacy totals remain available.
        public List<CommissionCurrencyTotal>? CurrencyTotals { get; set; }
    }

    public class CommissionCurrencyTotal
    {
        public string CurrencyCode { get; set; } = string.Empty;
        public string? AccountNumber { get; set; }
        public decimal Value { get; set; }
    }

    public class DashboardTotalsSummary
    {
        public int InternalTransfers { get; set; }
        public int CheckRequests { get; set; }
        public int CheckBookRequests { get; set; }
        public int Salaries { get; set; }
    }
}

