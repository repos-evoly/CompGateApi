// ─────────────────────────────────────────────────────────────────────────────
// CompGateApi.Core.Abstractions/ICertifiedBankStatementRequestRepository.cs
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface ICertifiedBankStatementRequestRepository
    {
        // COMPANY: by this company
        Task<IList<CertifiedBankStatementRequest>> GetAllByCompanyAsync(
            int companyId, string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountByCompanyAsync(
            int companyId, string? searchTerm, string? searchBy);

        // ADMIN: all
        Task<IList<CertifiedBankStatementRequest>> GetAllAsync(
            string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountAsync(
            string? searchTerm, string? searchBy);

        Task<CertifiedBankStatementRequest?> GetByIdAsync(int id);
        Task CreateAsync(CertifiedBankStatementRequest entity);
        Task UpdateAsync(CertifiedBankStatementRequest entity);
        Task DeleteAsync(int id);
    }
}
