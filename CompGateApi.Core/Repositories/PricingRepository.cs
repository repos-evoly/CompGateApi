using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
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
                var term = searchTerm.Trim();
                q = q.Where(p =>
                    (p.Description ?? "").Contains(term) ||
                    (p.AmountRule ?? "").Contains(term) ||
                    (p.GL1 ?? "").Contains(term) ||
                    (p.GL2 ?? "").Contains(term) ||
                    (p.GL3 ?? "").Contains(term) ||
                    (p.GL4 ?? "").Contains(term) ||
                    (p.DTC ?? "").Contains(term) ||
                    (p.CTC ?? "").Contains(term) ||
                    (p.DTC2 ?? "").Contains(term) ||
                    (p.CTC2 ?? "").Contains(term) ||
                    (p.NR2 ?? "").Contains(term)
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
                var term = searchTerm.Trim();
                q = q.Where(p =>
                    (p.Description ?? "").Contains(term) ||
                    (p.AmountRule ?? "").Contains(term) ||
                    (p.GL1 ?? "").Contains(term) ||
                    (p.GL2 ?? "").Contains(term) ||
                    (p.GL3 ?? "").Contains(term) ||
                    (p.GL4 ?? "").Contains(term) ||
                    (p.DTC ?? "").Contains(term) ||
                    (p.CTC ?? "").Contains(term) ||
                    (p.DTC2 ?? "").Contains(term) ||
                    (p.CTC2 ?? "").Contains(term) ||
                    (p.NR2 ?? "").Contains(term)
                );
            }

            if (page <= 0) page = 1;
            if (limit <= 0 || limit > 500) limit = 50;

            return await q.OrderByDescending(p => p.Id)
                          .Skip((page - 1) * limit)
                          .Take(limit)
                          .AsNoTracking()
                          .ToListAsync();
        }

        public async Task<Pricing> CreateAsync(Pricing entity)
        {
            var exists = await _db.TransactionCategories.AnyAsync(tc => tc.Id == entity.TrxCatId);
            if (!exists)
                throw new KeyNotFoundException($"TransactionCategory {entity.TrxCatId} not found.");

            // normalize minimal strings
            entity.AmountRule = string.IsNullOrWhiteSpace(entity.AmountRule) ? null : entity.AmountRule.Trim();
            entity.Description = string.IsNullOrWhiteSpace(entity.Description) ? null : entity.Description.Trim();
            entity.GL1 = string.IsNullOrWhiteSpace(entity.GL1) ? null : entity.GL1.Trim();
            entity.GL2 = string.IsNullOrWhiteSpace(entity.GL2) ? null : entity.GL2.Trim();
            entity.GL3 = string.IsNullOrWhiteSpace(entity.GL3) ? null : entity.GL3.Trim();
            entity.GL4 = string.IsNullOrWhiteSpace(entity.GL4) ? null : entity.GL4.Trim();
            entity.DTC = string.IsNullOrWhiteSpace(entity.DTC) ? null : entity.DTC.Trim();
            entity.CTC = string.IsNullOrWhiteSpace(entity.CTC) ? null : entity.CTC.Trim();
            entity.DTC2 = string.IsNullOrWhiteSpace(entity.DTC2) ? null : entity.DTC2.Trim();
            entity.CTC2 = string.IsNullOrWhiteSpace(entity.CTC2) ? null : entity.CTC2.Trim();
            entity.NR2 = string.IsNullOrWhiteSpace(entity.NR2) ? null : entity.NR2.Trim();

            _db.Set<Pricing>().Add(entity);
            await _db.SaveChangesAsync();
            return entity;
        }

        public async Task<bool> UpdateAsync(Pricing entity)
        {
            var current = await _db.Set<Pricing>().FirstOrDefaultAsync(x => x.Id == entity.Id);
            if (current == null) return false;

            if (current.TrxCatId != entity.TrxCatId)
            {
                var ok = await _db.TransactionCategories.AnyAsync(tc => tc.Id == entity.TrxCatId);
                if (!ok) throw new KeyNotFoundException($"TransactionCategory {entity.TrxCatId} not found.");
            }

            current.TrxCatId = entity.TrxCatId;
            current.PctAmt = entity.PctAmt;
            current.Price = entity.Price;
            current.AmountRule = string.IsNullOrWhiteSpace(entity.AmountRule) ? null : entity.AmountRule.Trim();
            current.Unit = entity.Unit;
            current.Description = string.IsNullOrWhiteSpace(entity.Description) ? null : entity.Description.Trim();

            current.GL1 = string.IsNullOrWhiteSpace(entity.GL1) ? null : entity.GL1.Trim();
            current.GL2 = string.IsNullOrWhiteSpace(entity.GL2) ? null : entity.GL2.Trim();
            current.GL3 = string.IsNullOrWhiteSpace(entity.GL3) ? null : entity.GL3.Trim();
            current.GL4 = string.IsNullOrWhiteSpace(entity.GL4) ? null : entity.GL4.Trim();

            current.DTC = string.IsNullOrWhiteSpace(entity.DTC) ? null : entity.DTC.Trim();
            current.CTC = string.IsNullOrWhiteSpace(entity.CTC) ? null : entity.CTC.Trim();
            current.DTC2 = string.IsNullOrWhiteSpace(entity.DTC2) ? null : entity.DTC2.Trim();
            current.CTC2 = string.IsNullOrWhiteSpace(entity.CTC2) ? null : entity.CTC2.Trim();

            current.NR2 = string.IsNullOrWhiteSpace(entity.NR2) ? null : entity.NR2.Trim();
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
