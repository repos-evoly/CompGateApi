using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface ICblRequestRepository
    {
        // Company
        Task<IList<CblRequest>> GetAllByUserAsync(int userId, string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountByUserAsync(int userId, string? searchTerm, string? searchBy);

        // Admin
        Task<IList<CblRequest>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountAsync(string? searchTerm, string? searchBy);

        Task<CblRequest?> GetByIdAsync(int id);
        Task CreateAsync(CblRequest entity);
        Task UpdateAsync(CblRequest entity);
        Task DeleteAsync(int id);

        Task<IList<CblRequest>> GetAllByCompanyAsync(
    int companyId, string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountByCompanyAsync(
            int companyId, string? searchTerm, string? searchBy);
    }
}