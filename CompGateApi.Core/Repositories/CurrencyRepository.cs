using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class CurrencyRepository : ICurrencyRepository
    {
        private readonly CompGateApiDbContext _context;
        public CurrencyRepository(CompGateApiDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(Currency currency)
        {
            await _context.Currencies.AddAsync(currency);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var currency = await _context.Currencies.FindAsync(id);
            if (currency != null)
            {
                _context.Currencies.Remove(currency);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IList<Currency>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit)
        {
            IQueryable<Currency> query = _context.Currencies.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "code":
                        query = query.Where(c => c.Code.Contains(searchTerm));
                        break;
                    case "description":
                        query = query.Where(c => c.Description.Contains(searchTerm));
                        break;
                    default:
                        query = query.Where(c => c.Code.Contains(searchTerm) || c.Description.Contains(searchTerm));
                        break;
                }
            }

            return await query.OrderBy(c => c.Id)
                              .Skip((page - 1) * limit)
                              .Take(limit)
                              .AsNoTracking()
                              .ToListAsync();
        }

        public async Task<int> GetCountAsync(string? searchTerm, string? searchBy)
        {
            IQueryable<Currency> query = _context.Currencies.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "code":
                        query = query.Where(c => c.Code.Contains(searchTerm));
                        break;
                    case "description":
                        query = query.Where(c => c.Description.Contains(searchTerm));
                        break;
                    default:
                        query = query.Where(c => c.Code.Contains(searchTerm) || c.Description.Contains(searchTerm));
                        break;
                }
            }

            return await query.CountAsync();
        }

        public async Task<Currency?> GetByIdAsync(int id)
        {
            return await _context.Currencies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task UpdateAsync(Currency currency)
        {
            _context.Currencies.Update(currency);
            await _context.SaveChangesAsync();
        }
    }
}
