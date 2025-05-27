// ─────────────────────────────────────────────────────────────────────────────
// CompGateApi.Data.Repositories/CertifiedBankStatementRequestRepository.cs
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
    public class CertifiedBankStatementRequestRepository : ICertifiedBankStatementRequestRepository
    {
        private readonly CompGateApiDbContext _context;
        public CertifiedBankStatementRequestRepository(CompGateApiDbContext context)
            => _context = context;

        // COMPANY: only this company’s requests
        public async Task<IList<CertifiedBankStatementRequest>> GetAllByCompanyAsync(
            int companyId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CertifiedBankStatementRequests
                            .Where(r => r.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "holder":
                        q = q.Where(r => r.AccountHolderName!.Contains(searchTerm));
                        break;
                    case "authname":
                        q = q.Where(r => r.AuthorizedOnTheAccountName!.Contains(searchTerm));
                        break;
                    case "account":
                        q = q.Where(r => r.AccountNumber.ToString().Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.AccountHolderName!.Contains(searchTerm) ||
                            r.AuthorizedOnTheAccountName!.Contains(searchTerm) ||
                            r.AccountNumber.ToString().Contains(searchTerm));
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
            var q = _context.CertifiedBankStatementRequests
                            .Where(r => r.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "holder":
                        q = q.Where(r => r.AccountHolderName!.Contains(searchTerm));
                        break;
                    case "authname":
                        q = q.Where(r => r.AuthorizedOnTheAccountName!.Contains(searchTerm));
                        break;
                    case "account":
                        q = q.Where(r => r.AccountNumber.ToString().Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.AccountHolderName!.Contains(searchTerm) ||
                            r.AuthorizedOnTheAccountName!.Contains(searchTerm) ||
                            r.AccountNumber.ToString().Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }

        // ADMIN: all requests
        public async Task<IList<CertifiedBankStatementRequest>> GetAllAsync(
            string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CertifiedBankStatementRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "holder":
                        q = q.Where(r => r.AccountHolderName!.Contains(searchTerm));
                        break;
                    case "authname":
                        q = q.Where(r => r.AuthorizedOnTheAccountName!.Contains(searchTerm));
                        break;
                    case "account":
                        q = q.Where(r => r.AccountNumber.ToString().Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.AccountHolderName!.Contains(searchTerm) ||
                            r.AuthorizedOnTheAccountName!.Contains(searchTerm) ||
                            r.AccountNumber.ToString().Contains(searchTerm));
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

        public async Task<int> GetCountAsync(
            string? searchTerm, string? searchBy)
        {
            var q = _context.CertifiedBankStatementRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "holder":
                        q = q.Where(r => r.AccountHolderName!.Contains(searchTerm));
                        break;
                    case "authname":
                        q = q.Where(r => r.AuthorizedOnTheAccountName!.Contains(searchTerm));
                        break;
                    case "account":
                        q = q.Where(r => r.AccountNumber.ToString().Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.AccountHolderName!.Contains(searchTerm) ||
                            r.AuthorizedOnTheAccountName!.Contains(searchTerm) ||
                            r.AccountNumber.ToString().Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }

        public async Task<CertifiedBankStatementRequest?> GetByIdAsync(int id)
            => await _context.CertifiedBankStatementRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

        public async Task CreateAsync(CertifiedBankStatementRequest entity)
        {
            _context.CertifiedBankStatementRequests.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(CertifiedBankStatementRequest entity)
        {
            _context.CertifiedBankStatementRequests.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _context.CertifiedBankStatementRequests.FindAsync(id);
            if (ent != null)
            {
                _context.CertifiedBankStatementRequests.Remove(ent);
                await _context.SaveChangesAsync();
            }
        }
    }
}
