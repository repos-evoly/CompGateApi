using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CompGateApi.Data.Repositories
{
    public class TransactionCategoryRepository : ITransactionCategoryRepository
    {
        private readonly CompGateApiDbContext _db;
        public TransactionCategoryRepository(CompGateApiDbContext db) => _db = db;

        public async Task<IList<TransactionCategory>> GetAllAsync() =>
            await _db.TransactionCategories.AsNoTracking().ToListAsync();

        public async Task<TransactionCategory?> GetByIdAsync(int id) =>
            await _db.TransactionCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

        public async Task<IList<ServicePackageDetail>> GetByServicePackageAsync(int servicePackageId) =>
            await _db.ServicePackageDetails
                     .Include(d => d.TransactionCategory)
                     .Where(d => d.ServicePackageId == servicePackageId)
                     .AsNoTracking()
                     .ToListAsync();

        public async Task CreateAsync(TransactionCategory cat)
        {
            _db.TransactionCategories.Add(cat);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(TransactionCategory cat)
        {
            _db.TransactionCategories.Update(cat);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var cat = await _db.TransactionCategories.FindAsync(id);
            if (cat == null) return;
            _db.TransactionCategories.Remove(cat);
            await _db.SaveChangesAsync();
        }
    }
}