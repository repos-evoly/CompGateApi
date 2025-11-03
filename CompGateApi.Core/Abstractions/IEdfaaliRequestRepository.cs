using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface IEdfaaliRequestRepository
    {
        // COMPANY scope
        Task<IList<EdfaaliRequest>> GetAllByCompanyAsync(int companyId, string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountByCompanyAsync(int companyId, string? searchTerm, string? searchBy);

        // ADMIN scope
        Task<IList<EdfaaliRequest>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountAsync(string? searchTerm, string? searchBy);

        Task<EdfaaliRequest?> GetByIdAsync(int id);
        Task CreateAsync(EdfaaliRequest entity);
        Task UpdateAsync(EdfaaliRequest entity);
        Task DeleteAsync(int id);
    }
}

