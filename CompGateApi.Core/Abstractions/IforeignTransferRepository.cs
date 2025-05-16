using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface IForeignTransferRepository
    {
        // Company/user
        Task<IList<ForeignTransfer>> GetAllByUserAsync(int userId, string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountByUserAsync(int userId, string? searchTerm, string? searchBy);

        // Admin
        Task<IList<ForeignTransfer>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountAsync(string? searchTerm, string? searchBy);

        Task<ForeignTransfer?> GetByIdAsync(int id);
        Task CreateAsync(ForeignTransfer req);
        Task UpdateAsync(ForeignTransfer req);
        Task DeleteAsync(int id);
    }
}
