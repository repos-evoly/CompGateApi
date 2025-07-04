// CompGateApi.Core.Abstractions/IFormStatusRepository.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface IFormStatusRepository
    {
        Task<IList<FormStatus>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountAsync(string? searchTerm, string? searchBy);
        Task<FormStatus?> GetByIdAsync(int id);
        Task CreateAsync(FormStatus formStatus);
        Task UpdateAsync(FormStatus formStatus);
        Task DeleteAsync(int id);
    }
}
