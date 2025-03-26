using BlockingApi.Data.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace BlockingApi.Core.Abstractions
{
    public interface ICustomerRepository
    {
        Task<List<Customer>> GetBlockedCustomers();
        Task<List<Customer>> GetUnblockedCustomers();
        Task<List<Customer>> SearchCustomers(string searchTerm);
    }
}
