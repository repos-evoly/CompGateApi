// CompGateApi.Core.Abstractions/ITransferLimitRepository.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface ITransferLimitRepository
    {
        Task<IList<TransferLimit>> GetAllAsync(int? servicePackageId = null,
                                              int? transactionCategoryId = null,
                                              int? currencyId = null,
                                              string? period = null);
        Task<TransferLimit?> GetByIdAsync(int id);
        Task CreateAsync(TransferLimit entity);
        Task UpdateAsync(TransferLimit entity);
        Task DeleteAsync(int id);
    }
}
