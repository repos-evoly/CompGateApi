using System.Threading.Tasks;
using System.Collections.Generic;
using BlockingApi.Data.Models;

namespace BlockingApi.Data.Abstractions
{
    public interface IExternalTransactionRepository
    {
        Task<List<Transaction>> GetExternalTransactionsAsync(int fromDate, int toDate, int limit, string branchCode, bool localCCY);
    }
}
