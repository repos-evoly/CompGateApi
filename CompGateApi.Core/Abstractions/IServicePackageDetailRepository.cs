// ─────────────────────────────────────────────────────────────────────────────
// CompGateApi.Core.Abstractions/IServicePackageDetailRepository.cs
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface IServicePackageDetailRepository
    {
        Task<IList<ServicePackageDetail>> GetAllAsync(int? servicePackageId = null, int? transactionCategoryId = null);
        Task<ServicePackageDetail?> GetByIdAsync(int id);
        Task CreateAsync(ServicePackageDetail entity);
        Task UpdateAsync(ServicePackageDetail entity);
        Task DeleteAsync(int id);
    }
}
