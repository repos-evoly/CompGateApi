using CompGateApi.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CompGateApi.Core.Abstractions
{
    public interface IServicePackageRepository
    {
        Task<IList<ServicePackage>> GetAllAsync();
        Task<ServicePackage?> GetByIdAsync(int id);
        Task CreateAsync(ServicePackage pkg);
        Task UpdateAsync(ServicePackage pkg);
        Task DeleteAsync(int id);
        Task<IList<ServicePackageDetail>> GetDetailsAsync(int packageId);
    }
}