// ── CompGateApi.Data.Repositories/VisaRequestRepository.cs ──────────────
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class VisaRequestRepository : IVisaRequestRepository
    {
        private readonly CompGateApiDbContext _context;
        public VisaRequestRepository(CompGateApiDbContext context) => _context = context;

        public async Task CreateAsync(VisaRequest entity)
        {
            _context.VisaRequests.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _context.VisaRequests.FindAsync(id);
            if (ent != null)
            {
                _context.VisaRequests.Remove(ent);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IList<VisaRequest>> GetAllByUserAsync(int userId, string? searchTerm, int page, int limit)
        {
            var q = _context.VisaRequests.Where(v => v.UserId == userId);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                q = q.Where(v =>
                    v.AccountHolderName!.Contains(searchTerm) ||
                    v.AccountNumber!.Contains(searchTerm) ||
                    v.Cbl!.Contains(searchTerm));
            }
            return await q
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetCountByUserAsync(int userId, string? searchTerm)
        {
            var q = _context.VisaRequests.Where(v => v.UserId == userId);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                q = q.Where(v =>
                    v.AccountHolderName!.Contains(searchTerm) ||
                    v.AccountNumber!.Contains(searchTerm) ||
                    v.Cbl!.Contains(searchTerm));
            }
            return await q.CountAsync();
        }

        public async Task<IList<VisaRequest>> GetAllAsync(string? searchTerm, int page, int limit)
        {
            var q = _context.VisaRequests.AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                q = q.Where(v =>
                    v.AccountHolderName!.Contains(searchTerm) ||
                    v.AccountNumber!.Contains(searchTerm) ||
                    v.Cbl!.Contains(searchTerm));
            }
            return await q
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetCountAsync(string? searchTerm)
        {
            var q = _context.VisaRequests.AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                q = q.Where(v =>
                    v.AccountHolderName!.Contains(searchTerm) ||
                    v.AccountNumber!.Contains(searchTerm) ||
                    v.Cbl!.Contains(searchTerm));
            }
            return await q.CountAsync();
        }

        public async Task<VisaRequest?> GetByIdAsync(int id)
            => await _context.VisaRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == id);

        public async Task UpdateAsync(VisaRequest entity)
        {
            _context.VisaRequests.Update(entity);
            await _context.SaveChangesAsync();
        }
    }
}
