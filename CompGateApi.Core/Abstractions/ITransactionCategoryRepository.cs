using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface ITransactionCategoryRepository
    {
        Task<IList<TransactionCategory>> GetAllAsync(string? searchTerm, int page, int limit);
        Task<int> GetCountAsync(string? searchTerm);
        Task<TransactionCategory?> GetByIdAsync(int id);
        Task CreateAsync(TransactionCategory entity);
        Task UpdateAsync(TransactionCategory entity);
        Task DeleteAsync(int id);
    }
}
