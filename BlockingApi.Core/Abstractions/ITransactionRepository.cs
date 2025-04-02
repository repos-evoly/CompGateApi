using BlockingApi.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlockingApi.Data.Abstractions
{
    public interface ITransactionRepository
    {
        // Get all transactions from the database
        Task<List<Transaction>> GetAllTransactionsAsync();

        // Get a transaction by ID
        Task<Transaction> GetTransactionByIdAsync(int id);

        // Add a new transaction to the database
        Task AddTransactionAsync(Transaction transaction);

        // Update an existing transaction in the database
        Task UpdateTransactionAsync(Transaction transaction);

        // Delete a transaction from the database
        Task DeleteTransactionAsync(int id);

        // Approve a transaction and update its status
        Task ApproveTransactionAsync(int transactionId, int userId);

        // Reject a transaction and update its status
        Task RejectTransactionAsync(int transactionId, int userId);

        // Escalate a transaction and update its status
        Task EscalateTransactionAsync(int transactionId, int userId);

        // Add a transaction flow record (e.g., for approval, rejection, escalation)
        Task AddTransactionFlowAsync(TransactionFlow transactionFlow);

        Task<List<TransactionFlow>> GetTransactionFlowsByTransactionIdAsync(int transactionId);

        Task<IEnumerable<Transaction>> GetEscalatedTransactionsAsync();

        Task<int> GetFlaggedTransactionsCountAsync();
        Task<int> GetHighValueTransactionsCountAsync();


    }
}
