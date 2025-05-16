using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface IBankAccountRepository
    {
        Task<IList<BankAccount>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit);
        Task<BankAccount?> GetByIdAsync(int id);
        Task CreateAsync(BankAccount account);
        Task UpdateAsync(BankAccount account);
        Task DeleteAsync(int id);
        Task<int> GetCountAsync(string? searchTerm, string? searchBy);
    }
}
