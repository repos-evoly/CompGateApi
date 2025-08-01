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
            .Where(b => b.CompanyId == companyId && !b.IsDeleted)
            .ToListAsync();
    }

    public async Task<Beneficiary?> GetByIdAsync(int id)
    {
        return await _db.Beneficiaries.FindAsync(id);
    }

    public async Task CreateAsync(Beneficiary entity)
    {
        _db.Beneficiaries.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Beneficiary entity)
    {
        _db.Beneficiaries.Update(entity);
        await _db.SaveChangesAsync();
    }
}
