// ─────────────────────────────────────────────────────────────────────────────
// CompGateApi.Core.Abstractions/ICheckBookRequestRepository.cs
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface ICheckBookRequestRepository
    {
        // Company‐scoped:
        Task<IList<CheckBookRequest>> GetAllByUserAsync(int userId, string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountByUserAsync(int userId, string? searchTerm, string? searchBy);

        // Admin‐scoped:
        Task<IList<CheckBookRequest>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountAsync(string? searchTerm, string? searchBy);

        Task<CheckBookRequest?> GetByIdAsync(int id);
        Task CreateAsync(CheckBookRequest entity);
        Task UpdateAsync(CheckBookRequest entity);
        Task DeleteAsync(int id);

        Task<IList<CheckBookRequest>> GetAllByCompanyAsync(
        int companyId,
        string? searchTerm,
        string? searchBy,
        int page,
        int limit);

        Task<int> GetCountByCompanyAsync(
            int companyId,
            string? searchTerm,
            string? searchBy);
    }
}
