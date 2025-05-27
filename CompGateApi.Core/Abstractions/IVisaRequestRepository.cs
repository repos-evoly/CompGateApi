// ── CompGateApi.Core.Abstractions/IVisaRequestRepository.cs ─────────────
using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface IVisaRequestRepository
    {
        // Company: own requests
        Task<IList<VisaRequest>> GetAllByUserAsync(int userId, string? searchTerm, int page, int limit);
        Task<int> GetCountByUserAsync(int userId, string? searchTerm);

        // Admin: all requests
        Task<IList<VisaRequest>> GetAllAsync(string? searchTerm, int page, int limit);
        Task<int> GetCountAsync(string? searchTerm);

        Task<VisaRequest?> GetByIdAsync(int id);
        Task CreateAsync(VisaRequest entity);
        Task UpdateAsync(VisaRequest entity);
        Task DeleteAsync(int id);

        // ── COMPANY only ────────────────────────────────────────────────
        Task<IList<VisaRequest>> GetAllByCompanyAsync(
             int userId,
             string? searchTerm,
             string? searchBy,
             int page,
             int limit);

        Task<int> GetCountByCompanyAsync(
            int userId,
            string? searchTerm,
            string? searchBy);

    }
}
