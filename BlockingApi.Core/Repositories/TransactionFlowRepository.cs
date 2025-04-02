using BlockingApi.Data.Context;
using BlockingApi.Data.Models;
using BlockingApi.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlockingApi.Data.Repositories
{
    public class TransactionFlowRepository : ITransactionFlowRepository
    {
        private readonly BlockingApiDbContext _context;

        public TransactionFlowRepository(BlockingApiDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<TransactionFlow>> GetTransactionFlowByTransactionIdAsync(int transactionId)
        {
            return await _context.TransactionFlows
                .Where(tf => tf.TransactionId == transactionId)
                .ToListAsync(); // Ensure this returns a collection
        }

        public async Task DeleteTransactionFlowAsync(int transactionFlowId)
        {
            var transactionFlow = await _context.TransactionFlows.FindAsync(transactionFlowId);
            if (transactionFlow != null)
            {
                _context.TransactionFlows.Remove(transactionFlow);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateTransactionFlowAsync(TransactionFlow transactionFlow)
        {
            _context.TransactionFlows.Update(transactionFlow);
            await _context.SaveChangesAsync();
        }
    }
}
