using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CompGateApi.Core.Abstractions
{
    public interface IPricingRepository
    {
        Task<Pricing?> GetByIdAsync(int id);
        Task<int> GetCountAsync(int? trxCatId, string? searchTerm);
        Task<List<Pricing>> GetAllAsync(int? trxCatId, string? searchTerm, int page, int limit);

        Task<Pricing> CreateAsync(Pricing entity);
        Task<bool> UpdateAsync(Pricing entity);
        Task<bool> DeleteAsync(int id);
    }
}
