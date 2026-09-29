using System.Reflection;
using System.Security.Claims;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Core.Startup;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using CompGateApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CompGateApi.Tests;

public sealed class LegacyTransferCompatibilityTests
{
    private static CompGateApiDbContext NewDb() => new(
        new DbContextOptionsBuilder<CompGateApiDbContext>()
            .UseSqlServer("Server=localhost;Database=NeverConnected;Integrated Security=true;TrustServerCertificate=true")
            .Options);

    [Fact]
    public void LegacyModelDoesNotReadOrWriteUnappliedBeneficiaryColumns()
    {
        using var db = NewDb();
        var entity = db.Model.FindEntityType(typeof(Beneficiary))!;
        foreach (var name in new[] { "PaymentRail", "RowVersion", "CreatedByUserId",
                     "InstitutionId", "InstitutionName", "ProviderInstitutionReference" })
            Assert.Null(entity.FindProperty(name));
        Assert.Null(entity.FindNavigation("CreatedByUser"));
        Assert.NotNull(entity.FindProperty("CompanyId"));
        Assert.NotNull(entity.FindProperty("AccountNumber"));
        Assert.NotNull(db.Model.FindEntityType(typeof(NotificationOutbox)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryQueriesSupportBothSchemasAndKeepCompanyAndArchiveFilters(bool upgraded)
    {
        using var db = NewDb();
        var query = LegacyBeneficiaryQueries.ForSchema(db, upgraded)
            .Where(b => b.CompanyId == 27 && !b.IsDeleted);
        var listSql = query.OrderBy(b => b.Id).Skip(0).Take(10).ToQueryString();
        var nameSql = query.Where(b => b.AccountNumber.Trim() == "123456")
            .Select(b => b.Name).ToQueryString();
        foreach (var sql in new[] { listSql, nameSql })
        {
            Assert.Contains("[CompanyId]", sql);
            Assert.Contains("[IsDeleted]", sql);
            Assert.DoesNotContain("[RowVersion]", sql);
            Assert.DoesNotContain("[InstitutionId]", sql);
            Assert.DoesNotContain("[CreatedByUserId]", sql);
            if (upgraded) Assert.Contains("[PaymentRail] = N'Normal'", sql);
            else Assert.DoesNotContain("[PaymentRail]", sql);
        }
    }

    [Fact]
    public void IntegrationRoutesAndWorkersAreNotRegistered()
    {
        var assembly = typeof(BeneficiaryEndpoints).Assembly;
        Assert.Null(assembly.GetType("CompGateApi.Endpoints.OnePayEndpoints"));
        Assert.Null(assembly.GetType("CompGateApi.Endpoints.LyPayEndpoints"));
        Assert.NotNull(assembly.GetType("CompGateApi.Endpoints.MobileInternalUserEndpoints"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Services.RegisterRepos(builder.Configuration, builder.Environment);
        Assert.DoesNotContain(builder.Services, d =>
            (d.ServiceType.FullName ?? "").Contains("OnePay") ||
            (d.ServiceType.FullName ?? "").Contains("LyPay") ||
            (d.ImplementationType?.FullName ?? "").Contains("OnePay") ||
            (d.ImplementationType?.FullName ?? "").Contains("LyPay"));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(ITransferRequestRepository));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IEmployeeSalaryRepository));
    }

    [Theory]
    [InlineData(PaymentRail.OnePay)]
    [InlineData(PaymentRail.LyPay)]
    [InlineData((PaymentRail)99)]
    public async Task RepositoryRejectsDisabledRailsBeforeAnyDatabaseAccess(PaymentRail rail)
    {
        using var db = NewDb();
        var repo = new BeneficiaryRepository(db);
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.GetPageByCompanyAsync(27, rail, null, null, 1, 10));
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.CreateAsync(new Beneficiary { PaymentRail = rail }));
    }

    [Fact]
    public async Task NormalCreateAndEditDoNotRequireProviderServicesOrRowVersion()
    {
        var repo = new MemoryBeneficiaries();
        var created = await BeneficiaryEndpoints.CreateCompanyBeneficiary(
            new BeneficiaryCreateDto { Name = " Recipient ", AccountNumber = " 123456 " },
            Context(), repo, Users(27));
        Assert.Equal(201, ((IStatusCodeHttpResult)created).StatusCode);
        Assert.Equal(27, repo.Item!.CompanyId);
        Assert.Equal("Recipient", repo.Item.Name);
        Assert.Equal("123456", repo.Item.AccountNumber);
        var updated = await BeneficiaryEndpoints.UpdateCompanyBeneficiary(1,
            new BeneficiaryUpdateDto { Name = "Updated", AccountNumber = "654321" },
            Context(), repo, Users(27));
        Assert.Equal(200, ((IStatusCodeHttpResult)updated).StatusCode);
        Assert.Equal("Updated", repo.Item.Name);
        Assert.Equal(1, repo.Updates);
    }

    [Theory]
    [InlineData(PaymentRail.OnePay)]
    [InlineData(PaymentRail.LyPay)]
    [InlineData((PaymentRail)99)]
    public async Task ApiRejectsRailCreateEditAndList(PaymentRail rail)
    {
        var repo = new MemoryBeneficiaries { Item = new Beneficiary { Id = 1, CompanyId = 27 } };
        var create = await BeneficiaryEndpoints.CreateCompanyBeneficiary(
            new BeneficiaryCreateDto { PaymentRail = rail }, Context(), repo, Users(27));
        var edit = await BeneficiaryEndpoints.UpdateCompanyBeneficiary(1,
            new BeneficiaryUpdateDto { PaymentRail = rail }, Context(), repo, Users(27));
        var list = await BeneficiaryEndpoints.GetCompanyBeneficiaries(Context(), repo, Users(27), rail.ToString());
        foreach (var result in new[] { create, edit, list })
            Assert.Equal(400, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Equal(0, repo.Creates);
        Assert.Equal(0, repo.Updates);
    }

    [Theory]
    [InlineData(28, false)]
    [InlineData(27, true)]
    public async Task CannotReadEditOrArchiveAnotherCompanyOrDeletedBeneficiary(int owner, bool deleted)
    {
        var repo = new MemoryBeneficiaries { Item = new Beneficiary { Id = 1, CompanyId = owner, IsDeleted = deleted } };
        var get = await BeneficiaryEndpoints.GetCompanyBeneficiaryById(1, Context(), repo, Users(27));
        var edit = await BeneficiaryEndpoints.UpdateCompanyBeneficiary(1,
            new BeneficiaryUpdateDto { Name = "Changed", AccountNumber = "123" }, Context(), repo, Users(27));
        var archive = await BeneficiaryEndpoints.ArchiveCompanyBeneficiary(1, Context(), repo, Users(27));
        foreach (var result in new[] { get, edit, archive })
            Assert.Equal(404, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Equal(0, repo.Updates);
    }

    [Fact]
    public async Task NormalArchiveIsStillSoftDelete()
    {
        var repo = new MemoryBeneficiaries { Item = new Beneficiary { Id = 1, CompanyId = 27 } };
        var result = await BeneficiaryEndpoints.ArchiveCompanyBeneficiary(1, Context(), repo, Users(27));
        Assert.Equal(204, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.True(repo.Item.IsDeleted);
        Assert.Equal(1, repo.Updates);
    }

    private static HttpContext Context() => new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("nameid", "4") }, "Test"))
    };

    private static IUserRepository Users(int company)
    {
        var result = DispatchProxy.Create<IUserRepository, UserProxy>();
        ((UserProxy)(object)result).Company = company;
        return result;
    }

    public class UserProxy : DispatchProxy
    {
        public int Company { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name == nameof(IUserRepository.GetUserByAuthId)
                ? Task.FromResult<UserDetailsDto?>(new UserDetailsDto { CompanyId = Company, AuthUserId = 4 })
                : throw new NotSupportedException(method.Name);
    }

    private sealed class MemoryBeneficiaries : IBeneficiaryRepository
    {
        public Beneficiary? Item;
        public int Creates;
        public int Updates;
        public Task<Beneficiary?> GetByIdAsync(int id) => Task.FromResult(Item);
        public Task CreateAsync(Beneficiary entity)
        { Creates++; entity.Id = 1; Item = entity; return Task.CompletedTask; }
        public Task UpdateAsync(Beneficiary entity, byte[]? expectedRowVersion = null)
        { Assert.Null(expectedRowVersion); Updates++; Item = entity; return Task.CompletedTask; }
        public Task<List<Beneficiary>> GetAllByCompanyAsync(int companyId) => throw new NotSupportedException();
        public Task<(List<Beneficiary> Items, int Total)> GetPageByCompanyAsync(
            int companyId, PaymentRail paymentRail, string? searchTerm, string? searchBy, int page, int limit) =>
            throw new NotSupportedException();
        public Task<bool> ActiveDestinationExistsAsync(int companyId, PaymentRail paymentRail,
            string institutionId, string accountNumber, int? excludingId = null) => throw new NotSupportedException();
    }
}
