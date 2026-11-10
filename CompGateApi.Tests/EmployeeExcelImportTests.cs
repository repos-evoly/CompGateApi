using AutoMapper;
using ClosedXML.Excel;
using CompGateApi.Core.Employees;
using CompGateApi.Core.Errors;
using CompGateApi.Core.Startup;
using CompGateApi.Data.Context;
using CompGateApi.Data.Migrations;
using CompGateApi.Data.Models;
using CompGateApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace CompGateApi.Tests;

public sealed class EmployeeExcelImportTests
{
    private const string Account = "0012345000001";
    private static CompGateApiDbContext NewDb() => new(new DbContextOptionsBuilder<CompGateApiDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Employee Existing(int id, bool deleted = false, string account = Account, int company = 1) => new()
    {
        Id = id, CompanyId = company, Name = $"Employee {id}", AccountNumber = account,
        AccountType = "account", Salary = 100, IsDeleted = deleted, Email = $"{id}@example.test",
        Phone = "123", EvoWallet = "existing-wallet", SendSalary = true, CanPost = true
    };

    private static EmployeeSalaryRepository Repo(CompGateApiDbContext db) => new(db, new NoHttp(),
        new MapperConfiguration(_ => { }).CreateMapper());

    private sealed class NoHttp : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("Import must not call another API.");
    }

    private static MemoryStream Excel(params (string Name, string Account, decimal Salary)[] rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Employees");
        sheet.Cell(1, 1).Value = "Name";
        sheet.Cell(1, 2).Value = "Account Number";
        sheet.Cell(1, 3).Value = "Salary";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Name;
            sheet.Cell(i + 2, 2).Value = rows[i].Account;
            sheet.Cell(i + 2, 3).Value = rows[i].Salary;
        }
        var stream = new MemoryStream();
        book.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task ImportPrefersActiveIdentityOverDeletedDuplicatesAndPreservesContactAndWallets()
    {
        await using var db = NewDb();
        db.Employees.AddRange(Existing(1), Existing(2, true), Existing(3, true));
        await db.SaveChangesAsync();
        using var excel = Excel(("Updated", Account, 250.125m));
        var result = await Repo(db).ImportEmployeesFromExcelAsync(1, excel);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.CreatedCount);
        var active = Assert.Single(await db.Employees.Where(e => !e.IsDeleted).ToListAsync());
        Assert.Equal(1, active.Id);
        Assert.Equal("Updated", active.Name);
        Assert.Equal(250.125m, active.Salary);
        Assert.Equal("1@example.test", active.Email);
        Assert.Equal("123", active.Phone);
        Assert.Equal("existing-wallet", active.EvoWallet);
        Assert.True((await db.Employees.FindAsync(2))!.IsDeleted);
    }

    [Fact]
    public async Task ImportReactivatesHighestDeletedIdWithoutCreatingOrMergingRecords()
    {
        await using var db = NewDb();
        db.Employees.AddRange(Existing(10, true), Existing(20, true));
        await db.SaveChangesAsync();
        using var excel = Excel(("Restored", Account, 50));
        var result = await Repo(db).ImportEmployeesFromExcelAsync(1, excel);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(20, (await db.Employees.SingleAsync(e => !e.IsDeleted)).Id);
        Assert.True((await db.Employees.FindAsync(10))!.IsDeleted);
        Assert.Equal(2, await db.Employees.CountAsync());
    }

    [Fact]
    public async Task AmbiguousActiveAccountsRejectEntireImportBeforeSaving()
    {
        await using var db = NewDb();
        db.Employees.AddRange(Existing(1), Existing(2, account: "001 2345000001.0"), Existing(3, account: "0012345000003"));
        await db.SaveChangesAsync();
        using var excel = Excel(("Would change", "0012345000003", 500), ("Conflict", Account, 200));
        var error = await Assert.ThrowsAsync<EmployeeAccountConflictException>(() => Repo(db).ImportEmployeesFromExcelAsync(1, excel));
        Assert.Contains("1, 2", error.Message);
        Assert.Equal("Employee 3", (await db.Employees.AsNoTracking().SingleAsync(e => e.Id == 3)).Name);
        Assert.Equal(3, await db.Employees.CountAsync(e => !e.IsDeleted));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyOrAllInvalidRowsCannotDeleteEmployees(bool invalid)
    {
        await using var db = NewDb();
        db.Employees.Add(Existing(1));
        await db.SaveChangesAsync();
        using var excel = invalid ? Excel(("Invalid", "123", 50)) : Excel();
        await Assert.ThrowsAsync<InvalidDataException>(() => Repo(db).ImportEmployeesFromExcelAsync(1, excel));
        Assert.False((await db.Employees.AsNoTracking().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task ValidImportCreatesUpdatesArchivesAndRetainsOtherCompanyRecords()
    {
        await using var db = NewDb();
        db.Employees.AddRange(Existing(1), Existing(2, account: "0012345000002"), Existing(3, company: 2));
        await db.SaveChangesAsync();
        using var excel = Excel(("Updated", Account, 123.4567m), ("New", "0012345000003", 300));
        var result = await Repo(db).ImportEmployeesFromExcelAsync(1, excel);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.DeletedCount);
        Assert.True((await db.Employees.FindAsync(2))!.IsDeleted);
        Assert.False((await db.Employees.FindAsync(3))!.IsDeleted);
        Assert.Equal(123.457m, (await db.Employees.FindAsync(1))!.Salary);
        var created = await db.Employees.SingleAsync(e => e.CompanyId == 1 && e.AccountNumber == "0012345000003");
        Assert.Null(created.Email);
        Assert.Null(created.Phone);
    }

    [Fact]
    public async Task DuplicateRowsInFileAreReportedOnceAndDoNotCreateExtraEmployees()
    {
        await using var db = NewDb();
        using var excel = Excel(("First", Account, 100), ("Second", Account, 200));
        var result = await Repo(db).ImportEmployeesFromExcelAsync(1, excel);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(3, Assert.Single(result.Errors).RowNumber);
        Assert.Equal("First", (await db.Employees.SingleAsync()).Name);
    }

    [Fact]
    public async Task ImportLeavesWalletOnlyEmployeesWithoutBankAccountsUntouched()
    {
        // UAT permits wallet-only legacy rows with NULL bank accounts.
        await using var db = new CompGateApiDbContext(new DbContextOptionsBuilder<CompGateApiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), options => options.EnableNullChecks(false)).Options);
        db.Employees.AddRange(Existing(1, account: null!), Existing(2, account: ""));
        await db.SaveChangesAsync();
        using var excel = Excel(("Bank employee", Account, 100));
        var result = await Repo(db).ImportEmployeesFromExcelAsync(1, excel);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(3, await db.Employees.CountAsync(e => !e.IsDeleted));
        var created = await Repo(db).CreateEmployeeAsync(1,
            new EmployeeCreateDto { Name = "Another bank employee", AccountNumber = "0012345000002", AccountType = "account" });
        Assert.Equal("0012345000002", created.AccountNumber);
    }

    [Fact]
    public async Task CreateRejectsNormalizedActiveDuplicateButAllowsSameAccountInAnotherCompany()
    {
        await using var db = NewDb();
        db.Employees.Add(Existing(1));
        await db.SaveChangesAsync();
        var dto = new EmployeeCreateDto { Name = "New", AccountNumber = " 001 2345000001.0 ", AccountType = "account" };
        await Assert.ThrowsAsync<EmployeeAccountConflictException>(() => Repo(db).CreateEmployeeAsync(1, dto));
        var created = await Repo(db).CreateEmployeeAsync(2, dto);
        Assert.Equal(Account, created.AccountNumber);
    }

    [Fact]
    public async Task UpdateAllowsOwnAccountAndRejectsAnotherActiveEmployeesAccount()
    {
        await using var db = NewDb();
        db.Employees.AddRange(Existing(1), Existing(2, account: "0012345000002"));
        await db.SaveChangesAsync();
        var dto = new EmployeeCreateDto { Name = "Renamed", AccountNumber = Account, AccountType = "account" };
        Assert.NotNull(await Repo(db).UpdateEmployeeAsync(1, 1, dto));
        await Assert.ThrowsAsync<EmployeeAccountConflictException>(() => Repo(db).UpdateEmployeeAsync(1, 2, dto));
        Assert.Equal("0012345000002", (await db.Employees.FindAsync(2))!.AccountNumber);
    }

    [Fact]
    public async Task UploadReturnsUsefulConflictThroughLegacyWrapperWithoutCallingAuthOrBank()
    {
        await using var db = NewDb();
        db.Users.Add(new User { Id = 1, AuthUserId = 6, CompanyId = 1, RoleId = 1, Email = "test@example.test" });
        db.Employees.AddRange(Existing(1), Existing(2));
        await db.SaveChangesAsync();
        using var excel = Excel(("Incoming", Account, 100));
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "6") }, "test"));
        ctx.Request.ContentType = "multipart/form-data; boundary=test";
        ctx.Request.Form = new FormCollection(new Dictionary<string, StringValues>(),
            new FormFileCollection { new FormFile(excel, 0, excel.Length, "files", "employees.xlsx") });
        ctx.Response.Body = new MemoryStream();
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        ctx.RequestServices = services;
        var middleware = new Always200ResponseMiddleware(async context =>
        {
            var result = await EmployeeSalaryEndpoints.ImportEmployeesFromExcel(context.Request, context, Repo(db),
                NullLogger<EmployeeSalaryEndpoints>.Instance, db);
            Assert.Equal(409, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            await result.ExecuteAsync(context);
        });
        await middleware.Invoke(ctx);
        ctx.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(ctx.Response.Body);
        Assert.Equal(200, ctx.Response.StatusCode);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(409, json.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("IDs: 1, 2", json.RootElement.GetProperty("message").GetString());
        Assert.Equal(2, await db.Employees.CountAsync(e => !e.IsDeleted));
    }

    [Fact]
    public void MigrationOnlyAddsGuardedActiveAccountIndexAndModelMatches()
    {
        var migration = new AddUniqueActiveEmployeeAccount { ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer" };
        Assert.Equal(2, migration.UpOperations.Count);
        Assert.Contains("THROW 51000", Assert.IsType<SqlOperation>(migration.UpOperations[0]).Sql);
        var operation = Assert.IsType<CreateIndexOperation>(migration.UpOperations[1]);
        Assert.True(operation.IsUnique);
        Assert.Equal("[IsDeleted] = 0 AND [AccountNumber] IS NOT NULL AND [AccountNumber] <> N''", operation.Filter);
        Assert.Equal(new[] { "CompanyId", "AccountNumber" }, operation.Columns);
        Assert.IsType<DropIndexOperation>(Assert.Single(migration.DownOperations));
        using var db = new CompGateApiDbContext(new DbContextOptionsBuilder<CompGateApiDbContext>()
            .UseSqlServer("Server=localhost;Database=NeverConnected;Integrated Security=true").Options);
        var index = db.Model.FindEntityType(typeof(Employee))!.GetIndexes()
            .Single(i => i.GetDatabaseName() == EmployeeAccountRules.ActiveAccountIndex);
        Assert.True(index.IsUnique);
        Assert.Equal(operation.Filter, index.GetFilter());
    }
}
