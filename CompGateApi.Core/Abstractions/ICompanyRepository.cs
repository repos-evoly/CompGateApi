// CompGateApi.Core.Abstractions/ICompanyRepository.cs
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using System.Threading.Tasks;

namespace CompGateApi.Core.Abstractions
{
    public interface ICompanyRepository
    {
        /// <summary>Lookup KYC data from the external KYC API.</summary>
        Task<KycDto?> LookupKycAsync(string companyCode);

        /// <summary>True if that code has a KYC record and can proceed to registration.</summary>
        Task<bool> CanRegisterCompanyAsync(string companyCode);

        /// <summary>Create the first “company admin” user under review.</summary>
        Task<CompanyRegistrationResult> RegisterCompanyAdminAsync(CompanyRegistrationDto dto);

        Task<bool> UpdateCompanyStatusAsync(string companyCode, CompanyStatusUpdateDto dto);

        Task<IList<CompanyListDto>> GetAllCompaniesAsync(
           string? searchTerm,
           RegistrationStatus? statusFilter,
           int page,
           int limit);
        Task<int> GetCompaniesCountAsync(string? searchTerm, RegistrationStatus? statusFilter);
        Task<Company?> GetByCodeAsync(string code);

        Task CreateAsync(Company company);
    }
}
