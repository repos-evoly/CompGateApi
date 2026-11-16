using AutoMapper;
using CompGateApi.Core.Dtos;
using CompGateApi.Core.Repositories;
using CompGateApi.Core.Transfers;
using CompGateApi.Data.Context;
using CompGateApi.Data.Migrations;
using CompGateApi.Data.Models;
using CompGateApi.Data.Repositories;
using CompGateApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CompGateApi.Tests;

public sealed class EurCommissionTests
{
    private const string Eur = "0010932701978";
    private const string Sender = "0010798000978";
    private const string Receiver = "0010739554978";
    private static CompGateApiDbContext NewDb() => new(new DbContextOptionsBuilder<CompGateApiDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Settings Accounts(string? eur = Eur) => new()
    {
        Id = 1, CommissionAccount = "0010932701434", CommissionAccountUSD = "0010932701840", CommissionAccountEUR = eur
    };

    private sealed class Bank : HttpMessageHandler, IHttpClientFactory
    {
        public List<JsonElement> Postings { get; } = new();
        public HttpClient CreateClient(string name) => new(this, false) { BaseAddress = new Uri("https://bank.example.test") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("GetCustomerInfo"))
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { Details = new { CustInfo = new[] { new { STCOD = "CD" } } } }, options: new JsonSerializerOptions()) };
            Assert.EndsWith("flexPostTransfer", request.RequestUri.AbsolutePath);
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Postings.Add(document.RootElement.Clone());
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { Header = new { ReturnCode = "success" } }, options: new JsonSerializerOptions()) };
        }
    }

    private static async Task Seed(CompGateApiDbContext db, int currency, bool recipient, decimal fee = 2.50m, string? eur = Eur)
    {
        db.Settings.Add(Accounts(eur));
        db.Currencies.Add(new Currency { Id = currency, Code = currency switch { 1 => "LYD", 2 => "USD", _ => "EUR" }, Rate = 1 });
        db.Companies.Add(new Company { Id = 1, Code = "798000", Name = "Test", ServicePackageId = 1, CommissionOnReceiver = recipient });
        db.ServicePackages.Add(new ServicePackage { Id = 1, Name = "Test" });
        db.TransactionCategories.Add(new TransactionCategory { Id = 2, Name = "InternalTransfer" });
        db.ServicePackageDetails.Add(new ServicePackageDetail
        {
            Id = 1, ServicePackageId = 1, TransactionCategoryId = 2,
            IsEnabledForPackage = true, B2BFixedFee = fee, B2BCommissionPct = 0,
            B2CFixedFee = fee, B2CCommissionPct = 0, B2BTransactionLimit = 0,
            B2CTransactionLimit = 0, B2BMaxAmount = 0, B2CMaxAmount = 0,
            B2BMinPercentage = 0, B2CMinPercentage = 0
        });
        await db.SaveChangesAsync();
    }

    private static TransferRequestCreateDto Request(int currency) => new()
    {
        FromAccount = Sender, ToAccount = Receiver, Amount = 100, CurrencyId = currency,
        CurrencyDesc = "EUR", TransactionCategoryId = 2
    };

    [Theory]
    [InlineData(1, "0010932701434", false)]
    [InlineData(2, "0010932701840", false)]
    [InlineData(3, Eur, false)]
    [InlineData(3, Eur, true)]
    public async Task BothPostingPathsSelectCurrencyAccountAndPreserveFeePayer(int currency, string expected, bool recipient)
    {
        foreach (var draft in new[] { false, true })
        {
            await using var db = NewDb();
            await Seed(db, currency, recipient);
            using var bank = new Bank();
            var repo = new TransferRequestRepository(db, bank, NullLogger<TransferRequestRepository>.Instance);
            if (draft)
            {
                var created = await repo.CreateDraftAsync(1, 1, 1, Request(currency), "unused");
                Assert.True(created.Success, created.Error);
                Assert.Empty(bank.Postings);
                var posted = await repo.ExecuteAsync(created.Entity!.Id, 1, 1, "unused");
                Assert.True(posted.Success, posted.Error);
            }
            else
            {
                var posted = await repo.CreateAsync(1, 1, 1, Request(currency), "unused");
                Assert.True(posted.Success, posted.Error);
            }
            var details = Assert.Single(bank.Postings).GetProperty("details");
            Assert.Equal(expected, details.GetProperty("@DSTACC2").GetString());
            Assert.Equal(recipient ? Receiver : Sender, details.GetProperty("@SRCACC2").GetString());
            Assert.Equal("Y", details.GetProperty("@APLYTRN2").GetString());
            Assert.Equal(currency == 1 ? "000000000002500" : "000000000000250", details.GetProperty("@TRFAMT2").GetString());
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    public async Task MissingOrInvalidEurAccountRejectsBothPathsWithoutBankPosting(string? eur)
    {
        await using var db = NewDb();
        await Seed(db, 3, false, eur: eur);
        using var bank = new Bank();
        var repo = new TransferRequestRepository(db, bank, NullLogger<TransferRequestRepository>.Instance);
        var direct = await repo.CreateAsync(1, 1, 1, Request(3), "unused");
        Assert.False(direct.Success);
        Assert.Contains("EUR commission account", direct.Error);
        Assert.Empty(await db.TransferRequests.ToListAsync());
        var created = await repo.CreateDraftAsync(1, 1, 1, Request(3), "unused");
        Assert.True(created.Success, created.Error);
        var posted = await repo.ExecuteAsync(created.Entity!.Id, 1, 1, "unused");
        Assert.False(posted.Success);
        Assert.Contains("EUR commission account", posted.Error);
        Assert.Equal("Pending", created.Entity.Status);
        Assert.Empty(bank.Postings);
    }

    [Fact]
    public async Task NoFeeEurTransferDoesNotRequireACommissionAccount()
    {
        await using var db = NewDb();
        await Seed(db, 3, false, fee: 0, eur: null);
        using var bank = new Bank();
        var repo = new TransferRequestRepository(db, bank, NullLogger<TransferRequestRepository>.Instance);
        var posted = await repo.CreateAsync(1, 1, 1, Request(3), "unused");
        Assert.True(posted.Success, posted.Error);
        var details = Assert.Single(bank.Postings).GetProperty("details");
        Assert.Equal("N", details.GetProperty("@APLYTRN2").GetString());
        Assert.Equal("", details.GetProperty("@DSTACC2").GetString());
    }

    [Fact]
    public async Task SettingsNormalizeFormattedEurAccountRejectInvalidAndLeaveOtherFieldsUnchanged()
    {
        await using var db = NewDb();
        db.Settings.Add(Accounts(null));
        await db.SaveChangesAsync();
        var repo = new SettingsRepository(db);
        var mapper = new MapperConfiguration(c => c.AddProfile<MappingConfig>()).CreateMapper();
        var invalid = await SettingsEndpoints.Patch(repo, mapper, new SettingsPatchDto { CommissionAccountEUR = "123", GlobalLimit = 99 });
        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(invalid).StatusCode);
        Assert.Null((await db.Settings.AsNoTracking().SingleAsync()).CommissionAccountEUR);
        var result = await SettingsEndpoints.Patch(repo, mapper, new SettingsPatchDto { CommissionAccountEUR = "0010-932701-978" });
        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        var saved = await db.Settings.AsNoTracking().SingleAsync();
        Assert.Equal(Eur, saved.CommissionAccountEUR);
        Assert.Equal("0010932701434", saved.CommissionAccount);
        Assert.Equal("0010932701840", saved.CommissionAccountUSD);
        Assert.Equal(0, saved.GlobalLimit);
        var dto = mapper.Map<SettingsDto>(saved);
        Assert.Equal(Eur, dto.CommissionAccountEUR);
        await SettingsEndpoints.Patch(repo, mapper, new SettingsPatchDto { GlobalLimit = 100 });
        Assert.Equal(Eur, (await db.Settings.SingleAsync()).CommissionAccountEUR);
    }

    [Fact]
    public async Task DashboardSeparatesCompletedUsdAndEurCommissionsAndAppliesDateFilter()
    {
        await using var db = NewDb();
        db.Settings.Add(Accounts());
        var codes = new[] { "LYD", "USD", "EUR" };
        for (var i = 0; i < 3; i++)
        {
            db.Currencies.Add(new Currency { Id = i + 1, Code = codes[i], Rate = 1 });
            db.TransferRequests.Add(new TransferRequest
            {
                CurrencyId = i + 1, Status = "Completed", CommissionAmount = (i + 1) * 10, RequestedAt = new DateTime(2026, 11, 9), TransactionCategoryId = 2
            });
        }
        db.TransferRequests.Add(new TransferRequest { CurrencyId = 3, Status = "Completed", CommissionAmount = 999, RequestedAt = new DateTime(2026, 11, 1), TransactionCategoryId = 2 });
        await db.SaveChangesAsync();
        var summary = await new DashboardRepository(db).GetCommissionSummaryAsync(new DateTime(2026, 11, 9), new DateTime(2026, 11, 9));
        var transfer = Assert.Single(summary.CommissionBoxes.Where(b => b.Type == "transfers"));
        Assert.Equal(10, transfer.LydValue);
        Assert.Equal(50, transfer.OtherCurrenciesValue); // preserved for old clients
        Assert.Equal(20, transfer.CurrencyTotals!.Single(t => t.CurrencyCode == "USD").Value);
        var eur = transfer.CurrencyTotals!.Single(t => t.CurrencyCode == "EUR");
        Assert.Equal(30, eur.Value);
        Assert.Equal(Eur, eur.AccountNumber);
    }

    [Fact]
    public void MigrationOnlyAddsNullableEurAccountAndDoesNotChangeOtherTablesOrSeedAccounts()
    {
        var migration = new AddEurCommissionAccount { ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer" };
        var operation = Assert.IsType<AddColumnOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("Settings", operation.Table);
        Assert.Equal("CommissionAccountEUR", operation.Name);
        Assert.True(operation.IsNullable);
        Assert.Equal(13, operation.MaxLength);
        Assert.Null(operation.DefaultValue);
        Assert.IsType<DropColumnOperation>(Assert.Single(migration.DownOperations));
    }
}
