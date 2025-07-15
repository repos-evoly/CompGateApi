// CompGateApi.Data.Repositories/CblRequestRepository.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;

namespace CompGateApi.Data.Repositories
{
    public class CblRequestRepository : ICblRequestRepository
    {
        private readonly CompGateApiDbContext _ctx;
        public CblRequestRepository(CompGateApiDbContext ctx) => _ctx = ctx;

        public async Task<IList<CblRequest>> GetAllByUserAsync(int userId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _ctx.CblRequests.Where(r => r.UserId == userId);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm));
                        break;
                    case "party":
                        q = q.Where(r => r.PartyName!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r => r.PartyName!.Contains(searchTerm) || r.Status!.Contains(searchTerm));
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
            var q = _ctx.CblRequests.Where(r => r.UserId == userId);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm));
                        break;
                    case "party":
                        q = q.Where(r => r.PartyName!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r => r.PartyName!.Contains(searchTerm) || r.Status!.Contains(searchTerm));
                        break;
                }
            }
            return await q.CountAsync();
        }

        public async Task<IList<CblRequest>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _ctx.CblRequests.AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm));
                        break;
                    case "party":
                        q = q.Where(r => r.PartyName!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r => r.PartyName!.Contains(searchTerm) || r.Status!.Contains(searchTerm));
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
            var q = _ctx.CblRequests.AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm));
                        break;
                    case "party":
                        q = q.Where(r => r.PartyName!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r => r.PartyName!.Contains(searchTerm) || r.Status!.Contains(searchTerm));
                        break;
                }
            }
            return await q.CountAsync();
        }

        public async Task<CblRequest?> GetByIdAsync(int id)
            => await _ctx.CblRequests
                .Include(r => r.Officials)
                .Include(r => r.Attachments)
                .Include(r => r.Signatures)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

        public async Task CreateAsync(CblRequest entity)
        {
            _ctx.CblRequests.Add(entity);
            await _ctx.SaveChangesAsync();
        }

        public async Task UpdateAsync(CblRequest entity)
        {
            _ctx.CblRequests.Update(entity);
            await _ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _ctx.CblRequests.FindAsync(id);
            if (ent != null)
            {
                _ctx.CblRequests.Remove(ent);
                await _ctx.SaveChangesAsync();
            }
        }

        public async Task<IList<CblRequest>> GetAllByCompanyAsync(
    int companyId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _ctx.CblRequests.Where(r => r.CompanyId == companyId);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm)); break;
                    case "party":
                        q = q.Where(r => r.PartyName!.Contains(searchTerm)); break;
                    default:
                        q = q.Where(r =>
                            r.PartyName!.Contains(searchTerm) ||
                            r.Status!.Contains(searchTerm));
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

        public async Task<int> GetCountByCompanyAsync(
            int companyId, string? searchTerm, string? searchBy)
        {
            var q = _ctx.CblRequests.Where(r => r.CompanyId == companyId);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm)); break;
                    case "party":
                        q = q.Where(r => r.PartyName!.Contains(searchTerm)); break;
                    default:
                        q = q.Where(r =>
                            r.PartyName!.Contains(searchTerm) ||
                            r.Status!.Contains(searchTerm));
                        break;
                }
            }
            return await q.CountAsync();
        }
    }
}