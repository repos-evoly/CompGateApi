using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class TransactionCategoryRepository : ITransactionCategoryRepository
    {
        private readonly CompGateApiDbContext _context;

        public TransactionCategoryRepository(CompGateApiDbContext context)
            => _context = context;

        public async Task<IList<TransactionCategory>> GetAllAsync(
            string? searchTerm, int page, int limit)
        {
            var q = _context.TransactionCategories.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                q = q.Where(c =>
                    c.Name.Contains(searchTerm!) ||
                    (c.Description != null && c.Description.Contains(searchTerm!)));
            }

            return await q
                .OrderBy(c => c.Name)
                .Skip((page - 1) * limit)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetCountAsync(string? searchTerm)
        {
            var q = _context.TransactionCategories.AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                q = q.Where(c =>
                    c.Name.Contains(searchTerm!) ||
                    (c.Description != null && c.Description.Contains(searchTerm!)));
            }
            return await q.CountAsync();
        }

        public async Task<TransactionCategory?> GetByIdAsync(int id)
            => await _context.TransactionCategories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == id);

        public async Task CreateAsync(TransactionCategory entity)
        {
            _context.TransactionCategories.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(TransactionCategory entity)
        {
            _context.TransactionCategories.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _context.TransactionCategories.FindAsync(id);
            if (ent != null)
            {
                _context.TransactionCategories.Remove(ent);
                await _context.SaveChangesAsync();
            }
        }
    }
}
