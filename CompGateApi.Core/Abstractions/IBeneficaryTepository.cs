using CompGateApi.Data.Models;

public interface IBeneficiaryRepository
{
    Task<List<Beneficiary>> GetAllByCompanyAsync(int companyId);
    Task<Beneficiary?> GetByIdAsync(int id);
    Task CreateAsync(Beneficiary entity);
    Task UpdateAsync(Beneficiary entity);
}
