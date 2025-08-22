// CompGateApi.Data.Repositories/VisaRepository.cs
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompGateApi.Data.Repositories
{
    public class VisaRepository : IVisaRepository
    {
        private readonly CompGateApiDbContext _db;

        public VisaRepository(CompGateApiDbContext db)
        {
            _db = db;
        }

        public async Task<List<Visa>> GetAllAsync(CancellationToken ct = default)
        {
            return await _db.Visas
                .AsNoTracking()
                .OrderBy(v => v.NameEn)
                .ToListAsync(ct);
        }

        public async Task<Visa?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _db.Visas
                .Include(v => v.Attachments)
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == id, ct);
        }

        public async Task<Visa> CreateAsync(Visa entity, CancellationToken ct = default)
        {
            _db.Visas.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task<Visa?> UpdateAsync(int id, Visa entity, CancellationToken ct = default)
        {
            var existing = await _db.Visas.FirstOrDefaultAsync(v => v.Id == id, ct);
            if (existing == null) return null;

            existing.NameEn = entity.NameEn;
            existing.NameAr = entity.NameAr;
            existing.Price = entity.Price;
            existing.DescriptionEn = entity.DescriptionEn;
            existing.DescriptionAr = entity.DescriptionAr;

            await _db.SaveChangesAsync(ct);
            return existing;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var existing = await _db.Visas.FirstOrDefaultAsync(v => v.Id == id, ct);
            if (existing == null) return false;

            _db.Visas.Remove(existing);
            await _db.SaveChangesAsync(ct);
            return true;
        }
    }
}
