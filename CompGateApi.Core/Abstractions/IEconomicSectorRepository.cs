using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface IEconomicSectorRepository
    {
        Task<IList<EconomicSector>> GetAllAsync(string? searchTerm, int page, int limit);
        Task<int> GetCountAsync(string? searchTerm);
        Task<EconomicSector?> GetByIdAsync(int id);
        Task CreateAsync(EconomicSector sector);
        Task UpdateAsync(EconomicSector sector);
        Task DeleteAsync(int id);
    }
}
