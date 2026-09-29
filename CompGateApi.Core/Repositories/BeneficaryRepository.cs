using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

public class BeneficiaryRepository : IBeneficiaryRepository
{
    private readonly CompGateApiDbContext _db;

    public BeneficiaryRepository(CompGateApiDbContext db)
    {
        _db = db;
    }

    public async Task<List<Beneficiary>> GetAllByCompanyAsync(int companyId)
    {
        return await (await LegacyBeneficiaryQueries.NormalAsync(_db))
            .AsNoTracking()
            .Where(b =>
                b.CompanyId == companyId &&
                !b.IsDeleted)
            .ToListAsync();
    }

    public async Task<(List<Beneficiary> Items, int Total)> GetPageByCompanyAsync(
        int companyId,
        PaymentRail paymentRail,
        string? searchTerm,
        string? searchBy,
        int page,
        int limit)
    {
        RequireNormalRail(paymentRail);
        var query = (await LegacyBeneficiaryQueries.NormalAsync(_db))
            .AsNoTracking()
            .Where(beneficiary =>
                beneficiary.CompanyId == companyId &&
                !beneficiary.IsDeleted);

        var search = searchTerm?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = searchBy?.Trim().ToLowerInvariant() switch
            {
                "name" => query.Where(beneficiary =>
                    beneficiary.Name.Contains(search)),
                "accountnumber" => query.Where(beneficiary =>
                    beneficiary.AccountNumber.Contains(search)),
                "bank" => query.Where(beneficiary =>
                    (beneficiary.Bank != null && beneficiary.Bank.Contains(search))),
                _ => query.Where(beneficiary =>
                    beneficiary.Name.Contains(search) ||
                    beneficiary.AccountNumber.Contains(search) ||
                    (beneficiary.Bank != null && beneficiary.Bank.Contains(search)))
            };
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(beneficiary => beneficiary.CreatedAt)
            .ThenByDescending(beneficiary => beneficiary.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Beneficiary?> GetByIdAsync(int id)
    {
        return await (await LegacyBeneficiaryQueries.NormalAsync(_db))
            .SingleOrDefaultAsync(b => b.Id == id && !b.IsDeleted);
    }

    // Retained only for dormant provider source compatibility; never query an absent rail column.
    public Task<bool> ActiveDestinationExistsAsync(
        int companyId, PaymentRail paymentRail, string institutionId,
        string accountNumber, int? excludingId = null) =>
        throw new NotSupportedException("Payment-rail beneficiary operations are disabled.");

    private static void RequireNormalRail(PaymentRail rail)
    {
        if (rail != PaymentRail.Normal)
            throw new NotSupportedException("OnePay and LyPay are disabled.");
    }

    public async Task CreateAsync(Beneficiary entity)
    {
        RequireNormalRail(entity.PaymentRail);
        _db.Beneficiaries.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Beneficiary entity, byte[]? expectedRowVersion = null)
    {
        RequireNormalRail(entity.PaymentRail);
        if (expectedRowVersion is not null)
            throw new NotSupportedException("Rail row-version updates are disabled.");

        _db.Beneficiaries.Update(entity);
        await _db.SaveChangesAsync();
    }
}
