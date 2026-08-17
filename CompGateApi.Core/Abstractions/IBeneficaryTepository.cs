using CompGateApi.Data.Models;

public interface IBeneficiaryRepository
{
    Task<List<Beneficiary>> GetAllByCompanyAsync(int companyId);
    Task<(List<Beneficiary> Items, int Total)> GetPageByCompanyAsync(
        int companyId,
        PaymentRail paymentRail,
        string? searchTerm,
        string? searchBy,
        int page,
        int limit);
    Task<Beneficiary?> GetByIdAsync(int id);
    Task<bool> ActiveDestinationExistsAsync(
        int companyId,
        PaymentRail paymentRail,
        string institutionId,
        string accountNumber,
        int? excludingId = null);
    Task CreateAsync(Beneficiary entity);
    Task UpdateAsync(Beneficiary entity, byte[]? expectedRowVersion = null);
}
