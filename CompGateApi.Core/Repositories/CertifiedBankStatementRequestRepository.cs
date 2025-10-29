// CompGateApi.Data.Repositories/CertifiedBankStatementRequestRepository.cs
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

        // ---------------- COMPANY SCOPE ----------------
        public async Task<IList<CertifiedBankStatementRequest>> GetAllByCompanyAsync(
            int companyId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CertifiedBankStatementRequests
                            .Where(r => r.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();

                switch ((searchBy ?? "").ToLower())
                {
                    case "holder":
                        q = q.Where(r => r.AccountHolderName != null &&
                                         r.AccountHolderName.ToLower().Contains(term));
                        break;

                    case "authname":
                        q = q.Where(r => r.AuthorizedOnTheAccountName != null &&
                                         r.AuthorizedOnTheAccountName.ToLower().Contains(term));
                        break;

                    case "account":
                        q = q.Where(r => r.AccountNumber != null && r.AccountNumber.ToLower().Contains(term));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.AccountHolderName != null && r.AccountHolderName.ToLower().Contains(term)) ||
                            (r.AuthorizedOnTheAccountName != null && r.AuthorizedOnTheAccountName.ToLower().Contains(term)) ||
                            (r.AccountNumber != null && r.AccountNumber.ToLower().Contains(term)));
                        break;
                }
            }

            return await q.OrderByDescending(r => r.CreatedAt)
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
                var term = searchTerm.Trim().ToLower();

                switch ((searchBy ?? "").ToLower())
                {
                    case "holder":
                        q = q.Where(r => r.AccountHolderName != null &&
                                         r.AccountHolderName.ToLower().Contains(term));
                        break;

                    case "authname":
                        q = q.Where(r => r.AuthorizedOnTheAccountName != null &&
                                         r.AuthorizedOnTheAccountName.ToLower().Contains(term));
                        break;

                    case "account":
                        q = q.Where(r => r.AccountNumber != null && r.AccountNumber.ToLower().Contains(term));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.AccountHolderName != null && r.AccountHolderName.ToLower().Contains(term)) ||
                            (r.AuthorizedOnTheAccountName != null && r.AuthorizedOnTheAccountName.ToLower().Contains(term)) ||
                            (r.AccountNumber != null && r.AccountNumber.ToLower().Contains(term)));
                        break;
                }
            }

            return await q.CountAsync();
        }

        // ---------------- ADMIN SCOPE ----------------
        public async Task<IList<CertifiedBankStatementRequest>> GetAllAsync(
            string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CertifiedBankStatementRequests
                            .Include(r => r.Company) // needed for Company filters
                            .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "code":
                    case "companycode":
                        q = q.Where(r => r.Company != null &&
                                         r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    case "company":
                    case "companyname":
                        q = q.Where(r => r.Company != null &&
                                         r.Company.Name != null &&
                                         EF.Functions.Like(r.Company.Name.ToLower(), like));
                        break;

                    case "holder":
                        q = q.Where(r => r.AccountHolderName != null &&
                                         EF.Functions.Like(r.AccountHolderName.ToLower(), like));
                        break;

                    case "authname":
                        q = q.Where(r => r.AuthorizedOnTheAccountName != null &&
                                         EF.Functions.Like(r.AuthorizedOnTheAccountName.ToLower(), like));
                        break;

                    case "account":
                        q = q.Where(r => r.AccountNumber != null &&
                                         EF.Functions.Like(r.AccountNumber.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.AccountHolderName != null && EF.Functions.Like(r.AccountHolderName.ToLower(), like)) ||
                            (r.AuthorizedOnTheAccountName != null && EF.Functions.Like(r.AuthorizedOnTheAccountName.ToLower(), like)) ||
                            (r.AccountNumber != null && EF.Functions.Like(r.AccountNumber.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Company != null && r.Company.Name != null && EF.Functions.Like(r.Company.Name.ToLower(), like)));
                        break;
                }
            }

            return await q.OrderByDescending(r => r.CreatedAt)
                          .Skip((page - 1) * limit)
                          .Take(limit)
                          .AsNoTracking()
                          .ToListAsync();
        }

        public async Task<int> GetCountAsync(string? searchTerm, string? searchBy)
        {
            var q = _context.CertifiedBankStatementRequests
                            .Include(r => r.Company)
                            .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "code":
                    case "companycode":
                        q = q.Where(r => r.Company != null &&
                                         r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    case "company":
                    case "companyname":
                        q = q.Where(r => r.Company != null &&
                                         r.Company.Name != null &&
                                         EF.Functions.Like(r.Company.Name.ToLower(), like));
                        break;

                    case "holder":
                        q = q.Where(r => r.AccountHolderName != null &&
                                         EF.Functions.Like(r.AccountHolderName.ToLower(), like));
                        break;

                    case "authname":
                        q = q.Where(r => r.AuthorizedOnTheAccountName != null &&
                                         EF.Functions.Like(r.AuthorizedOnTheAccountName.ToLower(), like));
                        break;

                    case "account":
                        q = q.Where(r => r.AccountNumber != null &&
                                         EF.Functions.Like(r.AccountNumber.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.AccountHolderName != null && EF.Functions.Like(r.AccountHolderName.ToLower(), like)) ||
                            (r.AuthorizedOnTheAccountName != null && EF.Functions.Like(r.AuthorizedOnTheAccountName.ToLower(), like)) ||
                            (r.AccountNumber != null && EF.Functions.Like(r.AccountNumber.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Company != null && r.Company.Name != null && EF.Functions.Like(r.Company.Name.ToLower(), like)));
                        break;
                }
            }

            return await q.AsNoTracking().CountAsync();
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
