using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class EconomicSectorRepository : IEconomicSectorRepository
    {
        private readonly CompGateApiDbContext _context;
        public EconomicSectorRepository(CompGateApiDbContext context)
        {
            _context = context;
        }

        public async Task<IList<EconomicSector>> GetAllAsync(string? searchTerm, int page, int limit)
        {
            IQueryable<EconomicSector> query = _context.EconomicSectors;

            if (!string.IsNullOrWhiteSpace(searchTerm))
                query = query.Where(e => e.Name.Contains(searchTerm) || (e.Description ?? "").Contains(searchTerm));

            return await query
                .OrderBy(e => e.Id)
                .Skip((page - 1) * limit)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetCountAsync(string? searchTerm)
        {
            IQueryable<EconomicSector> query = _context.EconomicSectors;

            if (!string.IsNullOrWhiteSpace(searchTerm))
                query = query.Where(e => e.Name.Contains(searchTerm) || (e.Description ?? "").Contains(searchTerm));

            return await query.CountAsync();
        }

        public async Task<EconomicSector?> GetByIdAsync(int id)
        {
            return await _context.EconomicSectors.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task CreateAsync(EconomicSector sector)
        {
            await _context.EconomicSectors.AddAsync(sector);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(EconomicSector sector)
        {
            _context.EconomicSectors.Update(sector);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _context.EconomicSectors.FindAsync(id);
            if (entity != null)
            {
                _context.EconomicSectors.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
    }
}
