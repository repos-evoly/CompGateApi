// ── CompGateApi.Data.Repositories/RtgsRequestRepository.cs ────────────────
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class RtgsRequestRepository : IRtgsRequestRepository
    {
        private readonly CompGateApiDbContext _context;
        public RtgsRequestRepository(CompGateApiDbContext context) => _context = context;

        // ---------------- COMPANY: only user’s own ----------------
        public async Task<IList<RtgsRequest>> GetAllByUserAsync(
            int userId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.RtgsRequests
                .Include(r => r.Company) // needed for Company.Code/Name filters
                .Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "paymenttype":
                        q = q.Where(r => r.PaymentType != null &&
                                         EF.Functions.Like(r.PaymentType.ToLower(), like));
                        break;
                    case "beneficiarybank":
                        q = q.Where(r => r.BeneficiaryBank != null &&
                                         EF.Functions.Like(r.BeneficiaryBank.ToLower(), like));
                        break;
                    case "code":
                        q = q.Where(r => r.Company != null && r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;
                    default:
                        q = q.Where(r =>
                            (r.ApplicantName != null && EF.Functions.Like(r.ApplicantName.ToLower(), like)) ||
                            (r.BeneficiaryName != null && EF.Functions.Like(r.BeneficiaryName.ToLower(), like)) ||
                            (r.BranchName != null && EF.Functions.Like(r.BranchName.ToLower(), like)) ||
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

        public async Task<int> GetCountByUserAsync(
            int userId, string? searchTerm, string? searchBy)
        {
            var q = _context.RtgsRequests
                .Include(r => r.Company)
                .Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "paymenttype":
                        q = q.Where(r => r.PaymentType != null &&
                                         EF.Functions.Like(r.PaymentType.ToLower(), like));
                        break;
                    case "beneficiarybank":
                        q = q.Where(r => r.BeneficiaryBank != null &&
                                         EF.Functions.Like(r.BeneficiaryBank.ToLower(), like));
                        break;
                    case "code":
                        q = q.Where(r => r.Company != null && r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;
                    default:
                        q = q.Where(r =>
                            (r.ApplicantName != null && EF.Functions.Like(r.ApplicantName.ToLower(), like)) ||
                            (r.BeneficiaryName != null && EF.Functions.Like(r.BeneficiaryName.ToLower(), like)) ||
                            (r.BranchName != null && EF.Functions.Like(r.BranchName.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Company != null && r.Company.Name != null && EF.Functions.Like(r.Company.Name.ToLower(), like)));
                        break;
                }
            }

            return await q.AsNoTracking().CountAsync();
        }

        // ---------------- ADMIN: all requests ----------------
        public async Task<IList<RtgsRequest>> GetAllAsync(
            string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.RtgsRequests
                .Include(r => r.Company)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "paymenttype":
                        q = q.Where(r => r.PaymentType != null &&
                                         EF.Functions.Like(r.PaymentType.ToLower(), like));
                        break;
                    case "beneficiarybank":
                        q = q.Where(r => r.BeneficiaryBank != null &&
                                         EF.Functions.Like(r.BeneficiaryBank.ToLower(), like));
                        break;
                    case "code":
                        q = q.Where(r => r.Company != null && r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;
                    default:
                        q = q.Where(r =>
                            (r.ApplicantName != null && EF.Functions.Like(r.ApplicantName.ToLower(), like)) ||
                            (r.BeneficiaryName != null && EF.Functions.Like(r.BeneficiaryName.ToLower(), like)) ||
                            (r.BranchName != null && EF.Functions.Like(r.BranchName.ToLower(), like)) ||
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
            var q = _context.RtgsRequests
                .Include(r => r.Company)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "paymenttype":
                        q = q.Where(r => r.PaymentType != null &&
                                         EF.Functions.Like(r.PaymentType.ToLower(), like));
                        break;
                    case "beneficiarybank":
                        q = q.Where(r => r.BeneficiaryBank != null &&
                                         EF.Functions.Like(r.BeneficiaryBank.ToLower(), like));
                        break;
                    case "code":
                        q = q.Where(r => r.Company != null && r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;
                    default:
                        q = q.Where(r =>
                            (r.ApplicantName != null && EF.Functions.Like(r.ApplicantName.ToLower(), like)) ||
                            (r.BeneficiaryName != null && EF.Functions.Like(r.BeneficiaryName.ToLower(), like)) ||
                            (r.BranchName != null && EF.Functions.Like(r.BranchName.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Company != null && r.Company.Name != null && EF.Functions.Like(r.Company.Name.ToLower(), like)));
                        break;
                }
            }

            return await q.AsNoTracking().CountAsync();
        }

        // ---------------- COMPANY: by company ----------------
        public async Task<IList<RtgsRequest>> GetAllByCompanyAsync(
            int companyId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.RtgsRequests
                .Include(r => r.Company)
                .Where(r => r.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "paymenttype":
                        q = q.Where(r => r.PaymentType != null &&
                                         EF.Functions.Like(r.PaymentType.ToLower(), like));
                        break;
                    case "beneficiarybank":
                        q = q.Where(r => r.BeneficiaryBank != null &&
                                         EF.Functions.Like(r.BeneficiaryBank.ToLower(), like));
                        break;
                    case "code": // supported, though companyId already scopes company
                        q = q.Where(r => r.Company != null && r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;
                    default:
                        q = q.Where(r =>
                            (r.ApplicantName != null && EF.Functions.Like(r.ApplicantName.ToLower(), like)) ||
                            (r.BeneficiaryName != null && EF.Functions.Like(r.BeneficiaryName.ToLower(), like)) ||
                            (r.BranchName != null && EF.Functions.Like(r.BranchName.ToLower(), like)) ||
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

        public async Task<int> GetCountByCompanyAsync(
            int companyId, string? searchTerm, string? searchBy)
        {
            var q = _context.RtgsRequests
                .Include(r => r.Company)
                .Where(r => r.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "paymenttype":
                        q = q.Where(r => r.PaymentType != null &&
                                         EF.Functions.Like(r.PaymentType.ToLower(), like));
                        break;
                    case "beneficiarybank":
                        q = q.Where(r => r.BeneficiaryBank != null &&
                                         EF.Functions.Like(r.BeneficiaryBank.ToLower(), like));
                        break;
                    case "code":
                        q = q.Where(r => r.Company != null && r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;
                    default:
                        q = q.Where(r =>
                            (r.ApplicantName != null && EF.Functions.Like(r.ApplicantName.ToLower(), like)) ||
                            (r.BeneficiaryName != null && EF.Functions.Like(r.BeneficiaryName.ToLower(), like)) ||
                            (r.BranchName != null && EF.Functions.Like(r.BranchName.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Company != null && r.Company.Name != null && EF.Functions.Like(r.Company.Name.ToLower(), like)));
                        break;
                }
            }

            return await q.AsNoTracking().CountAsync();
        }

        public async Task<RtgsRequest?> GetByIdAsync(int id)
            => await _context.RtgsRequests
                .Include(r => r.Company) // optional: handy if caller needs company fields
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

        public async Task CreateAsync(RtgsRequest entity)
        {
            _context.RtgsRequests.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(RtgsRequest entity)
        {
            _context.RtgsRequests.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _context.RtgsRequests.FindAsync(id);
            if (ent != null)
            {
                _context.RtgsRequests.Remove(ent);
                await _context.SaveChangesAsync();
            }
        }
    }
}
