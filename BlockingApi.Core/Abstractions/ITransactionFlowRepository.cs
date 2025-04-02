using System.Collections.Generic;
using System.Threading.Tasks;
using BlockingApi.Data.Models;

namespace BlockingApi.Data.Abstractions
{
    public interface ITransactionFlowRepository
    {
        Task<IEnumerable<TransactionFlow>> GetTransactionFlowByTransactionIdAsync(int transactionId);
        Task UpdateTransactionFlowAsync(TransactionFlow transactionFlow);
        Task DeleteTransactionFlowAsync(int transactionFlowId);
    }
}
