// CompGateApi.Data.Repositories/TransferLimitRepository.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class TransferLimitRepository : ITransferLimitRepository
    {
        private readonly CompGateApiDbContext _ctx;
        public TransferLimitRepository(CompGateApiDbContext ctx)
            => _ctx = ctx;

        public async Task<IList<TransferLimit>> GetAllAsync(
            int? servicePackageId = null,
            int? transactionCategoryId = null,
            int? currencyId = null,
            string? period = null)
        {
            var q = _ctx.TransferLimits.AsQueryable();

            if (servicePackageId.HasValue)
                q = q.Where(l => l.ServicePackageId == servicePackageId.Value);
            if (transactionCategoryId.HasValue)
                q = q.Where(l => l.TransactionCategoryId == transactionCategoryId.Value);
            if (currencyId.HasValue)
                q = q.Where(l => l.CurrencyId == currencyId.Value);
            if (!string.IsNullOrWhiteSpace(period))
                q = q.Where(l => l.Period.ToString() == period);

            return await q
                .OrderBy(l => l.ServicePackageId)
                .ThenBy(l => l.TransactionCategoryId)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<TransferLimit?> GetByIdAsync(int id)
            => await _ctx.TransferLimits
                          .AsNoTracking()
                          .FirstOrDefaultAsync(l => l.Id == id);

        public async Task CreateAsync(TransferLimit entity)
        {
            _ctx.TransferLimits.Add(entity);
            await _ctx.SaveChangesAsync();
        }

        public async Task UpdateAsync(TransferLimit entity)
        {
            _ctx.TransferLimits.Update(entity);
            await _ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _ctx.TransferLimits.FindAsync(id);
            if (ent != null)
            {
                _ctx.TransferLimits.Remove(ent);
                await _ctx.SaveChangesAsync();
            }
        }
    }
}
