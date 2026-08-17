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
        return await _db.Beneficiaries
            .AsNoTracking()
            .Where(b =>
                b.CompanyId == companyId &&
                b.PaymentRail == PaymentRail.Normal &&
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
        var query = _db.Beneficiaries
            .AsNoTracking()
            .Where(beneficiary =>
                beneficiary.CompanyId == companyId &&
                beneficiary.PaymentRail == paymentRail &&
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
                    (beneficiary.Bank != null && beneficiary.Bank.Contains(search)) ||
                    (beneficiary.InstitutionName != null && beneficiary.InstitutionName.Contains(search)) ||
                    (beneficiary.InstitutionId != null && beneficiary.InstitutionId.Contains(search))),
                _ => query.Where(beneficiary =>
                    beneficiary.Name.Contains(search) ||
                    beneficiary.AccountNumber.Contains(search) ||
                    (beneficiary.InstitutionId != null && beneficiary.InstitutionId.Contains(search)) ||
                    (beneficiary.InstitutionName != null && beneficiary.InstitutionName.Contains(search)) ||
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
        return await _db.Beneficiaries.FindAsync(id);
    }

    public async Task<bool> ActiveDestinationExistsAsync(
        int companyId,
        PaymentRail paymentRail,
        string institutionId,
        string accountNumber,
        int? excludingId = null)
    {
        return await _db.Beneficiaries.AnyAsync(beneficiary =>
            beneficiary.CompanyId == companyId &&
            beneficiary.PaymentRail == paymentRail &&
            !beneficiary.IsDeleted &&
            beneficiary.InstitutionId == institutionId &&
            beneficiary.AccountNumber == accountNumber &&
            (!excludingId.HasValue || beneficiary.Id != excludingId.Value));
    }

    public async Task CreateAsync(Beneficiary entity)
    {
        _db.Beneficiaries.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Beneficiary entity, byte[]? expectedRowVersion = null)
    {
        if (expectedRowVersion is not null)
            _db.Entry(entity).Property(beneficiary => beneficiary.RowVersion).OriginalValue = expectedRowVersion;

        _db.Beneficiaries.Update(entity);
        await _db.SaveChangesAsync();
    }
}
