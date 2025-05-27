// CompGateApi.Core.Abstractions/IRtgsRequestRepository.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface IRtgsRequestRepository
    {
        // Company‐scoped
        Task<IList<RtgsRequest>> GetAllByUserAsync(int userId, string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountByUserAsync(int userId, string? searchTerm, string? searchBy);

        // Admin‐scoped
        Task<IList<RtgsRequest>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountAsync(string? searchTerm, string? searchBy);

        Task<RtgsRequest?> GetByIdAsync(int id);
        Task CreateAsync(RtgsRequest entity);
        Task UpdateAsync(RtgsRequest entity);
        Task DeleteAsync(int id);

        Task<IList<RtgsRequest>> GetAllByCompanyAsync(
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
