// CompGateApi.Data.Repositories/CheckRequestRepository.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class CheckRequestRepository : ICheckRequestRepository
    {
        private readonly CompGateApiDbContext _context;
        public CheckRequestRepository(CompGateApiDbContext context) => _context = context;

        public async Task CreateAsync(CheckRequest req)
        {
            _context.CheckRequests.Add(req);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _context.CheckRequests.FindAsync(id);
            if (ent != null)
            {
                _context.CheckRequests.Remove(ent);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IList<CheckRequest>> GetAllByUserAsync(int userId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CheckRequests
                            .Include(r => r.LineItems)
                            .Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "customer":
                        q = q.Where(r => r.CustomerName!.Contains(searchTerm));
                        break;
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.CustomerName!.Contains(searchTerm) ||
                            r.AccountNum!.Contains(searchTerm) ||
                            r.Beneficiary!.Contains(searchTerm));
                        break;
                }
            }

            return await q
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetCountByUserAsync(int userId, string? searchTerm, string? searchBy)
        {
            var q = _context.CheckRequests.Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "customer":
                        q = q.Where(r => r.CustomerName!.Contains(searchTerm));
                        break;
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.CustomerName!.Contains(searchTerm) ||
                            r.AccountNum!.Contains(searchTerm) ||
                            r.Beneficiary!.Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }

        public async Task<IList<CheckRequest>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CheckRequests.Include(r => r.LineItems).AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "customer":
                        q = q.Where(r => r.CustomerName!.Contains(searchTerm));
                        break;
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.CustomerName!.Contains(searchTerm) ||
                            r.AccountNum!.Contains(searchTerm) ||
                            r.Beneficiary!.Contains(searchTerm));
                        break;
                }
            }

            return await q
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetCountAsync(string? searchTerm, string? searchBy)
        {
            var q = _context.CheckRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "customer":
                        q = q.Where(r => r.CustomerName!.Contains(searchTerm));
                        break;
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.CustomerName!.Contains(searchTerm) ||
                            r.AccountNum!.Contains(searchTerm) ||
                            r.Beneficiary!.Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }

        public async Task<CheckRequest?> GetByIdAsync(int id)
            => await _context.CheckRequests
                      .Include(r => r.LineItems)
                      .AsNoTracking()
                      .FirstOrDefaultAsync(r => r.Id == id);

        public async Task UpdateAsync(CheckRequest req)
        {
            _context.CheckRequests.Update(req);
            await _context.SaveChangesAsync();
        }
    }
}
