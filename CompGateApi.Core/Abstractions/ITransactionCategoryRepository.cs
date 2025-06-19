using CompGateApi.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CompGateApi.Core.Abstractions
{
    public interface ITransactionCategoryRepository
    {
        Task<IList<TransactionCategory>> GetAllAsync();
        Task<TransactionCategory?> GetByIdAsync(int id);
        Task<IList<ServicePackageDetail>> GetByServicePackageAsync(int servicePackageId);

        Task CreateAsync(TransactionCategory cat);
        Task UpdateAsync(TransactionCategory cat);
        Task DeleteAsync(int id);
    }
}
