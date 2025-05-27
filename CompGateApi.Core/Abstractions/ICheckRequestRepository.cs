// CompGateApi.Core.Abstractions/ICheckRequestRepository.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface ICheckRequestRepository
    {
        // Company: only own requests
        Task<IList<CheckRequest>> GetAllByUserAsync(int userId, string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountByUserAsync(int userId, string? searchTerm, string? searchBy);

        // Admin: all requests
        Task<IList<CheckRequest>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountAsync(string? searchTerm, string? searchBy);

        Task<CheckRequest?> GetByIdAsync(int id);

        // company‐scoped filtering
        Task<IList<CheckRequest>> GetAllByCompanyAsync(
            int companyId, string? searchTerm, string? searchBy, int page, int limit);

        Task<int> GetCountByCompanyAsync(
            int companyId, string? searchTerm, string? searchBy);

        Task CreateAsync(CheckRequest req);
        Task UpdateAsync(CheckRequest req);
        Task DeleteAsync(int id);

        
    }
}
