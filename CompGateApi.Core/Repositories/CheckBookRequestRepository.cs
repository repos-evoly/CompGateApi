// ─────────────────────────────────────────────────────────────────────────────
// CompGateApi.Data.Repositories/CheckBookRequestRepository.cs
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class CheckBookRequestRepository : ICheckBookRequestRepository
    {
        private readonly CompGateApiDbContext _context;
        public CheckBookRequestRepository(CompGateApiDbContext context)
            => _context = context;

        // COMPANY: only this user’s requests
        public async Task<IList<CheckBookRequest>> GetAllByUserAsync(
            int userId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CheckBookRequests
                            .Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm));
                        break;
                    case "branch":
                        q = q.Where(r => r.Branch!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.FullName!.Contains(searchTerm) ||
                            r.AccountNumber!.Contains(searchTerm) ||
                            r.Branch!.Contains(searchTerm));
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

        public async Task<int> GetCountByUserAsync(
            int userId, string? searchTerm, string? searchBy)
        {
            var q = _context.CheckBookRequests
                            .Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm));
                        break;
                    case "branch":
                        q = q.Where(r => r.Branch!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.FullName!.Contains(searchTerm) ||
                            r.AccountNumber!.Contains(searchTerm) ||
                            r.Branch!.Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }

        // ADMIN: all requests
        public async Task<IList<CheckBookRequest>> GetAllAsync(
            string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CheckBookRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm));
                        break;
                    case "branch":
                        q = q.Where(r => r.Branch!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.FullName!.Contains(searchTerm) ||
                            r.AccountNumber!.Contains(searchTerm) ||
                            r.Branch!.Contains(searchTerm));
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
            var q = _context.CheckBookRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm));
                        break;
                    case "branch":
                        q = q.Where(r => r.Branch!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.FullName!.Contains(searchTerm) ||
                            r.AccountNumber!.Contains(searchTerm) ||
                            r.Branch!.Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }

        public async Task<CheckBookRequest?> GetByIdAsync(int id)
            => await _context.CheckBookRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

        public async Task CreateAsync(CheckBookRequest entity)
        {
            _context.CheckBookRequests.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(CheckBookRequest entity)
        {
            _context.CheckBookRequests.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _context.CheckBookRequests.FindAsync(id);
            if (ent != null)
            {
                _context.CheckBookRequests.Remove(ent);
                await _context.SaveChangesAsync();
            }
        }
    }
}
