using BlockingApi.Data.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace BlockingApi.Core.Abstractions
{
    public interface ICustomerRepository
    {
        Task<List<Customer>> GetBlockedCustomers(string? search, string? searchBy, int page, int limit);
        Task<List<Customer>> GetUnblockedCustomers(string? search, string? searchBy, int page, int limit);
        Task<List<Customer>> SearchCustomers(string searchTerm);
        Task<int> GetBlockedAccountsCountAsync();
        Task<int> GetBlockedUsersTodayCountAsync();
    }
}
