using CompGateApi.Core.Repositories;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CompGateApi.Tests;

public sealed class DashboardCommissionTests
{
    private static readonly DateTime ReportDay = new(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);

    private static async Task<CompGateApiDbContext> NewDb()
    {
        var db = new CompGateApiDbContext(new DbContextOptionsBuilder<CompGateApiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Currencies.AddRange(
            new Currency { Id = 1, Code = "LYD" },
            new Currency { Id = 2, Code = "USD" },
            new Currency { Id = 3, Code = "EUR" });
        db.Settings.Add(new Settings
        {
            Id = 1,
            CommissionAccount = "0010932701434",
            CommissionAccountUSD = "0010932701840",
            CommissionAccountEUR = "0010932701978"
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static TransferRequest Transfer(int id, int currency, string status, decimal commission,
        DateTime? requestedAt = null, int category = 2, decimal amount = 10m) => new()
    {
        Id = id,
        UserId = 1,
        CompanyId = 1,
        ServicePackageId = 1,
        TransactionCategoryId = category,
        CurrencyId = currency,
        FromAccount = "0015758899003",
        ToAccount = "0012775062003",
        Amount = amount,
        CommissionAmount = commission,
        Status = status,
        RequestedAt = requestedAt ?? ReportDay.AddHours(12)
    };

    [Fact]
    public async Task TwoPendingEuroDraftsDoNotAppearAsCollectedCommissionOrCompletedTransfers()
    {
        await using var db = await NewDb();
        db.TransferRequests.AddRange(Transfer(44, 3, "Pending", 5m), Transfer(45, 3, "Pending", 5m));
        await db.SaveChangesAsync();
        var repo = new DashboardRepository(db);

        var summary = await repo.GetCommissionSummaryAsync(null, null);
        var transfers = Assert.Single(summary.CommissionBoxes, box => box.Type == "transfers");
        var euro = Assert.Single(transfers.CurrencyTotals!, total => total.CurrencyCode == "EUR");
        Assert.Equal(0m, euro.Value);
        Assert.Equal("0010932701978", euro.AccountNumber);
        Assert.Equal(0m, transfers.OtherCurrenciesValue);
        Assert.Equal(0, (await repo.GetTotalsAsync(null, null)).InternalTransfers);
        Assert.Equal(2, await db.TransferRequests.CountAsync());
        Assert.All(await db.TransferRequests.AsNoTracking().ToListAsync(), transfer =>
        {
            Assert.Equal("Pending", transfer.Status);
            Assert.Equal(5m, transfer.CommissionAmount);
            Assert.Null(transfer.BankReference);
        });
    }

    [Fact]
    public async Task CurrencyBreakdownAndLegacyTotalsIncludeOnlyCompletedCommissions()
    {
        await using var db = await NewDb();
        db.TransferRequests.AddRange(
            Transfer(1, 1, "Completed", 7m),
            Transfer(2, 2, "Completed", 3m),
            Transfer(3, 3, "Completed", 2m),
            Transfer(4, 3, "Completed", 0m));
        var id = 5;
        foreach (var currency in new[] { 1, 2, 3 })
            foreach (var status in new[] { "Pending", "Failed", "Rejected", "Cancelled", "Processing" })
                db.TransferRequests.Add(Transfer(id++, currency, status, 100m));
        await db.SaveChangesAsync();
        var repo = new DashboardRepository(db);

        var summary = await repo.GetCommissionSummaryAsync(null, null);
        var transfers = Assert.Single(summary.CommissionBoxes, box => box.Type == "transfers");
        Assert.Equal(7m, transfers.LydValue);
        Assert.Equal(5m, transfers.OtherCurrenciesValue);
        var values = transfers.CurrencyTotals!.ToDictionary(total => total.CurrencyCode, total => total.Value);
        Assert.Equal(7m, values["LYD"]);
        Assert.Equal(3m, values["USD"]);
        Assert.Equal(2m, values["EUR"]);
        Assert.Equal(3, (await repo.GetTotalsAsync(null, null)).InternalTransfers);
    }

    [Fact]
    public async Task PendingCommissionAppearsOnlyAfterTransferBecomesCompleted()
    {
        await using var db = await NewDb();
        var transfer = Transfer(1, 3, "Pending", 5m);
        db.TransferRequests.Add(transfer);
        await db.SaveChangesAsync();
        var repo = new DashboardRepository(db);

        var before = await repo.GetCommissionSummaryAsync(null, null);
        Assert.Equal(0m, before.CommissionBoxes.Single(box => box.Type == "transfers").OtherCurrenciesValue);

        transfer.Status = "Completed";
        await db.SaveChangesAsync();

        var after = await repo.GetCommissionSummaryAsync(null, null);
        Assert.Equal(5m, after.CommissionBoxes.Single(box => box.Type == "transfers").OtherCurrenciesValue);
        Assert.Equal(1, (await repo.GetTotalsAsync(null, null)).InternalTransfers);
    }

    [Fact]
    public async Task DateRangeStillIncludesTheWholeSelectedDayForCompletedCommissions()
    {
        await using var db = await NewDb();
        db.TransferRequests.AddRange(
            Transfer(1, 3, "Completed", 100m, ReportDay.AddTicks(-1)),
            Transfer(2, 3, "Completed", 2m, ReportDay),
            Transfer(3, 3, "Completed", 3m, ReportDay.AddDays(1).AddTicks(-1)),
            Transfer(4, 3, "Completed", 100m, ReportDay.AddDays(1)),
            Transfer(5, 3, "Pending", 100m, ReportDay.AddHours(12)));
        await db.SaveChangesAsync();
        var repo = new DashboardRepository(db);

        var summary = await repo.GetCommissionSummaryAsync(ReportDay, ReportDay);
        Assert.Equal(5m, summary.CommissionBoxes.Single(box => box.Type == "transfers").OtherCurrenciesValue);
        Assert.Equal(2, (await repo.GetTotalsAsync(ReportDay, ReportDay)).InternalTransfers);
    }

    [Fact]
    public async Task StatementServiceFeesAlsoExcludeUncompletedRequests()
    {
        await using var db = await NewDb();
        db.TransferRequests.AddRange(
            Transfer(1, 1, "Completed", 0m, category: 9, amount: 12m),
            Transfer(2, 3, "Completed", 0m, category: 9, amount: 2m),
            Transfer(3, 1, "Pending", 0m, category: 9, amount: 100m),
            Transfer(4, 3, "Failed", 0m, category: 9, amount: 100m));
        await db.SaveChangesAsync();

        var summary = await new DashboardRepository(db).GetCommissionSummaryAsync(null, null);
        var statements = Assert.Single(summary.CommissionBoxes, box => box.Type == "statement");
        Assert.Equal(12m, statements.LydValue);
        Assert.Equal(2m, statements.OtherCurrenciesValue);
    }
}
