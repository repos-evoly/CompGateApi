// ─────────────────────────────────────────────────────────────────────────────
// CompGateApi.Core.Abstractions/IServicePackageRepository.cs
// ─────────────────────────────────────────────────────────────────────────────
using CompGateApi.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CompGateApi.Core.Abstractions
{
    public interface IServicePackageRepository
    {
        // CRUD on ServicePackage
        Task<IList<ServicePackage>> GetAllAsync();
        Task<ServicePackage?> GetByIdAsync(int id);
        Task CreateAsync(ServicePackage pkg);
        Task UpdateAsync(ServicePackage pkg);
        Task DeleteAsync(int id);

        // Optionally, load details & limits
        Task<IList<ServicePackageDetail>> GetDetailsAsync(int packageId);
        Task<IList<TransferLimit>> GetLimitsAsync(int packageId);
    }
}
