using BlockingApi.Data.Abstractions;
using BlockingApi.Data.Context;
using BlockingApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace BlockingApi.Data.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly BlockingApiDbContext _context;

        public TransactionRepository(BlockingApiDbContext context)
        {
            _context = context;
        }

        // Get all transactions from the database
        public async Task<List<Transaction>> GetAllTransactionsAsync()
        {
            return await _context.Transactions.ToListAsync();
        }

        // Get a transaction by ID
        public async Task<Transaction> GetTransactionByIdAsync(int id)
        {
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction == null)
            {
                throw new KeyNotFoundException($"Transaction with ID {id} was not found.");
            }
            return transaction;
        }

        // Add a new transaction to the database
        public async Task AddTransactionAsync(Transaction transaction)
        {
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
        }

        // Update an existing transaction in the database
        public async Task UpdateTransactionAsync(Transaction transaction)
        {
            _context.Transactions.Update(transaction);
            await _context.SaveChangesAsync();
        }

        // Delete a transaction from the database
        public async Task DeleteTransactionAsync(int id)
        {
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction != null)
            {
                _context.Transactions.Remove(transaction);
                await _context.SaveChangesAsync();
            }
        }

        // Approve a transaction and update its status
        public async Task ApproveTransactionAsync(int transactionId, int userId)
        {
            var transaction = await _context.Transactions.FindAsync(transactionId);
            if (transaction != null)
            {
                transaction.Status = "Approved";
                transaction.ApprovedByUserId = userId;
                await _context.SaveChangesAsync();

                // Log the approval in the transaction flow
                var transactionFlow = new TransactionFlow
                {
                    TransactionId = transaction.Id,
                    FromUserId = userId,
                    Action = "Approved",
                    ActionDate = DateTime.UtcNow,
                    Remark = $"Approved by user {userId}",
                    CanReturn = true
                };

                _context.TransactionFlows.Add(transactionFlow);
                await _context.SaveChangesAsync();
            }
        }

        // Reject a transaction and update its status
        public async Task RejectTransactionAsync(int transactionId, int userId)
        {
            var transaction = await _context.Transactions.FindAsync(transactionId);
            if (transaction != null)
            {
                transaction.Status = "Rejected";
                transaction.ApprovedByUserId = userId; // Assuming the user who rejected is saved
                await _context.SaveChangesAsync();

                // Log the rejection in the transaction flow
                var transactionFlow = new TransactionFlow
                {
                    TransactionId = transaction.Id,
                    FromUserId = userId,
                    Action = "Rejected",
                    ActionDate = DateTime.UtcNow,
                    Remark = $"Rejected by user {userId}",
                    CanReturn = false
                };

                _context.TransactionFlows.Add(transactionFlow);
                await _context.SaveChangesAsync();
            }
        }

        // Escalate a transaction and update its status
        public async Task EscalateTransactionAsync(int transactionId, int userId)
        {
            var transaction = await _context.Transactions.FindAsync(transactionId);
            if (transaction != null)
            {
                // Mark the transaction as escalated
                transaction.Status = "Escalated";
                await _context.SaveChangesAsync();

                // Log the escalation in the transaction flow
                var transactionFlow = new TransactionFlow
                {
                    TransactionId = transaction.Id,
                    FromUserId = userId,
                    Action = "Escalated",
                    ActionDate = DateTime.UtcNow,
                    Remark = $"Escalated by user {userId}",
                    CanReturn = false
                };

                _context.TransactionFlows.Add(transactionFlow);
                await _context.SaveChangesAsync();
            }
        }

        // Add a transaction flow record (e.g., for approval, rejection, escalation)
        public async Task AddTransactionFlowAsync(TransactionFlow transactionFlow)
        {
            _context.TransactionFlows.Add(transactionFlow);
            await _context.SaveChangesAsync();
        }

        public async Task<List<TransactionFlow>> GetTransactionFlowsByTransactionIdAsync(int transactionId)
        {
            return await _context.TransactionFlows
                .Where(tf => tf.TransactionId == transactionId)
                .Include(tf => tf.FromUser) // Include FromUser to get user details
                .Include(tf => tf.ToUser)   // Include ToUser to get user details
                .ToListAsync();
        }
    }
}
