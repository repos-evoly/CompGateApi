// CompGateApi.Core.Abstractions/IRepresentativeRepository.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface IRepresentativeRepository
    {
        Task<IList<Representative>> GetAllByCompanyAsync(int companyId, string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountByCompanyAsync(int companyId, string? searchTerm, string? searchBy);
        Task<Representative?> GetByIdAsync(int id);
        Task CreateAsync(Representative representative);
        Task UpdateAsync(Representative representative);
        Task DeleteAsync(int id);
    }
}
