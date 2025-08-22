using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompGateApi.Data.Repositories
{
    public class PricingRepository : IPricingRepository
    {
        private readonly CompGateApiDbContext _db;
        private readonly ILogger<PricingRepository> _log;

        public PricingRepository(CompGateApiDbContext db, ILogger<PricingRepository> log)
        {
            _db = db;
            _log = log;
        }

        public async Task<Pricing?> GetByIdAsync(int id)
            => await _db.Set<Pricing>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == id);

        public async Task<int> GetCountAsync(int? trxCatId, string? searchTerm)
        {
            var q = _db.Set<Pricing>().AsQueryable();

            if (trxCatId.HasValue)
                q = q.Where(p => p.TrxCatId == trxCatId.Value);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                q = q.Where(p =>
                    (p.Description ?? "").Contains(searchTerm) ||
                    (p.SGL1 ?? "").Contains(searchTerm) ||
                    (p.DGL1 ?? "").Contains(searchTerm) ||
                    (p.SGL2 ?? "").Contains(searchTerm) ||
                    (p.DGL2 ?? "").Contains(searchTerm) ||
                    (p.DTC ?? "").Contains(searchTerm) ||
                    (p.CTC ?? "").Contains(searchTerm) ||
                    (p.DTC2 ?? "").Contains(searchTerm) ||
                    (p.CTC2 ?? "").Contains(searchTerm) ||
                    (p.NR2 ?? "").Contains(searchTerm)
                );
            }

            return await q.CountAsync();
        }

        public async Task<List<Pricing>> GetAllAsync(int? trxCatId, string? searchTerm, int page, int limit)
        {
            var q = _db.Set<Pricing>().AsQueryable();

            if (trxCatId.HasValue)
                q = q.Where(p => p.TrxCatId == trxCatId.Value);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                q = q.Where(p =>
                    (p.Description ?? "").Contains(searchTerm) ||
                    (p.SGL1 ?? "").Contains(searchTerm) ||
                    (p.DGL1 ?? "").Contains(searchTerm) ||
                    (p.SGL2 ?? "").Contains(searchTerm) ||
                    (p.DGL2 ?? "").Contains(searchTerm) ||
                    (p.DTC ?? "").Contains(searchTerm) ||
                    (p.CTC ?? "").Contains(searchTerm) ||
                    (p.DTC2 ?? "").Contains(searchTerm) ||
                    (p.CTC2 ?? "").Contains(searchTerm) ||
                    (p.NR2 ?? "").Contains(searchTerm)
                );
            }

            return await q.OrderByDescending(p => p.Id)
                          .Skip((page - 1) * limit)
                          .Take(limit)
                          .AsNoTracking()
                          .ToListAsync();
        }

        public async Task<Pricing> CreateAsync(Pricing entity)
        {
            // Validate FK to TransactionCategory to avoid FK exception later
            var exists = await _db.TransactionCategories.AnyAsync(tc => tc.Id == entity.TrxCatId);
            if (!exists)
                throw new KeyNotFoundException($"TransactionCategory {entity.TrxCatId} not found.");

            _db.Set<Pricing>().Add(entity);
            await _db.SaveChangesAsync();
            return entity;
        }

        public async Task<bool> UpdateAsync(Pricing entity)
        {
            var current = await _db.Set<Pricing>().FirstOrDefaultAsync(x => x.Id == entity.Id);
            if (current == null) return false;

            // optional FK validation if TrxCatId changed
            if (current.TrxCatId != entity.TrxCatId)
            {
                var ok = await _db.TransactionCategories.AnyAsync(tc => tc.Id == entity.TrxCatId);
                if (!ok) throw new KeyNotFoundException($"TransactionCategory {entity.TrxCatId} not found.");
            }

            current.TrxCatId = entity.TrxCatId;
            current.PctAmt = entity.PctAmt;
            current.Price = entity.Price;
            current.Description = entity.Description;

            current.SGL1 = entity.SGL1;
            current.DGL1 = entity.DGL1;
            current.SGL2 = entity.SGL2;
            current.DGL2 = entity.DGL2;

            current.DTC = entity.DTC;
            current.CTC = entity.CTC;
            current.DTC2 = entity.DTC2;
            current.CTC2 = entity.CTC2;

            current.NR2 = entity.NR2;
            current.APPLYTR2 = entity.APPLYTR2;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var current = await _db.Set<Pricing>().FirstOrDefaultAsync(x => x.Id == id);
            if (current == null) return false;

            _db.Remove(current);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
