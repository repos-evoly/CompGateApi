// CompGateApi.Data.Repositories/RepresentativeRepository.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class RepresentativeRepository : IRepresentativeRepository
    {
        private readonly CompGateApiDbContext _context;

        public RepresentativeRepository(CompGateApiDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(Representative representative)
        {
            await _context.Representatives.AddAsync(representative);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var rep = await _context.Representatives.FindAsync(id);
            if (rep != null)
            {
                rep.IsActive = false;
                _context.Representatives.Update(rep);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IList<Representative>> GetAllByCompanyAsync(int companyId, string? searchTerm, string? searchBy, int page, int limit)
        {
            IQueryable<Representative> query = _context.Representatives
                                                      .Where(r => r.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "name":
                        query = query.Where(r => r.Name.Contains(searchTerm));
                        break;
                    case "number":
                        query = query.Where(r => r.Number.Contains(searchTerm));
                        break;
                    case "passportnumber":
                        query = query.Where(r => r.PassportNumber.Contains(searchTerm));
                        break;
                    default:
                        query = query.Where(r =>
                            r.Name.Contains(searchTerm) ||
                            r.Number.Contains(searchTerm) ||
                            r.PassportNumber.Contains(searchTerm));
                        break;
                }
            }

            return await query.OrderBy(r => r.Id)
                              .Skip((page - 1) * limit)
                              .Take(limit)
                              .AsNoTracking()
                              .ToListAsync();
        }

        public async Task<int> GetCountByCompanyAsync(int companyId, string? searchTerm, string? searchBy)
        {
            IQueryable<Representative> query = _context.Representatives
                                                      .Where(r => r.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "name":
                        query = query.Where(r => r.Name.Contains(searchTerm));
                        break;
                    case "number":
                        query = query.Where(r => r.Number.Contains(searchTerm));
                        break;
                    case "passportnumber":
                        query = query.Where(r => r.PassportNumber.Contains(searchTerm));
                        break;
                    default:
                        query = query.Where(r =>
                            r.Name.Contains(searchTerm) ||
                            r.Number.Contains(searchTerm) ||
                            r.PassportNumber.Contains(searchTerm));
                        break;
                }
            }

            return await query.CountAsync();
        }

        public async Task<Representative?> GetByIdAsync(int id)
        {
            return await _context.Representatives
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task UpdateAsync(Representative representative)
        {
            _context.Representatives.Update(representative);
            await _context.SaveChangesAsync();
        }
    }
}
