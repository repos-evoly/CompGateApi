using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class ForeignTransferRepository : IForeignTransferRepository
    {
        private readonly CompGateApiDbContext _context;
        public ForeignTransferRepository(CompGateApiDbContext context) => _context = context;

        public async Task CreateAsync(ForeignTransfer req)
        {
            _context.ForeignTransferRequests.Add(req);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _context.ForeignTransferRequests.FindAsync(id);
            if (ent != null)
            {
                _context.ForeignTransferRequests.Remove(ent);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IList<ForeignTransfer>> GetAllByUserAsync(int userId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.ForeignTransferRequests.Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "tobank":
                        q = q.Where(r => r.ToBank!.Contains(searchTerm));
                        break;
                    case "beneficiary":
                        q = q.Where(r => r.BeneficiaryName!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.ToBank!.Contains(searchTerm) ||
                            r.BeneficiaryName!.Contains(searchTerm) ||
                            r.PurposeOfTransfer!.Contains(searchTerm));
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
            var q = _context.ForeignTransferRequests.Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "tobank":
                        q = q.Where(r => r.ToBank!.Contains(searchTerm));
                        break;
                    case "beneficiary":
                        q = q.Where(r => r.BeneficiaryName!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.ToBank!.Contains(searchTerm) ||
                            r.BeneficiaryName!.Contains(searchTerm) ||
                            r.PurposeOfTransfer!.Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }

        public async Task<IList<ForeignTransfer>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.ForeignTransferRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "tobank":
                        q = q.Where(r => r.ToBank!.Contains(searchTerm));
                        break;
                    case "beneficiary":
                        q = q.Where(r => r.BeneficiaryName!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.ToBank!.Contains(searchTerm) ||
                            r.BeneficiaryName!.Contains(searchTerm) ||
                            r.PurposeOfTransfer!.Contains(searchTerm));
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
            var q = _context.ForeignTransferRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "tobank":
                        q = q.Where(r => r.ToBank!.Contains(searchTerm));
                        break;
                    case "beneficiary":
                        q = q.Where(r => r.BeneficiaryName!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.ToBank!.Contains(searchTerm) ||
                            r.BeneficiaryName!.Contains(searchTerm) ||
                            r.PurposeOfTransfer!.Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }

        public async Task<ForeignTransfer?> GetByIdAsync(int id)
            => await _context.ForeignTransferRequests
                             .AsNoTracking()
                             .FirstOrDefaultAsync(r => r.Id == id);

        public async Task UpdateAsync(ForeignTransfer req)
        {
            _context.ForeignTransferRequests.Update(req);
            await _context.SaveChangesAsync();
        }

        public async Task<IList<ForeignTransfer>> GetAllByCompanyAsync(
    int companyId,
    string? searchTerm,
    string? searchBy,
    int page,
    int limit)
        {
            var q = _context.ForeignTransferRequests
                        .Where(r => r.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm));
                        break;
                    case "beneficiary":
                        q = q.Where(r => r.BeneficiaryName!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r => r.BeneficiaryName!.Contains(searchTerm)
                                          || r.Status!.Contains(searchTerm));
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
            int companyId,
            string? searchTerm,
            string? searchBy)
        {
            var q = _context.ForeignTransferRequests
                        .Where(r => r.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "status":
                        q = q.Where(r => r.Status!.Contains(searchTerm));
                        break;
                    case "beneficiary":
                        q = q.Where(r => r.BeneficiaryName!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r => r.BeneficiaryName!.Contains(searchTerm)
                                          || r.Status!.Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }
    }
}
