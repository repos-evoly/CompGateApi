// CompGateApi.Data.Repositories/ForeignTransferRepository.cs
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

        // ---------------- COMPANY: by user ----------------
        public async Task<IList<ForeignTransfer>> GetAllByUserAsync(
            int userId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.ForeignTransferRequests
                .Include(r => r.Company) // needed for Company.Code/Name filters
                .Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "tobank":
                        q = q.Where(r => r.ToBank != null &&
                                         EF.Functions.Like(r.ToBank.ToLower(), like));
                        break;

                    case "beneficiary":
                        q = q.Where(r => r.BeneficiaryName != null &&
                                         EF.Functions.Like(r.BeneficiaryName.ToLower(), like));
                        break;

                    case "code":
                        q = q.Where(r => r.Company != null && r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.ToBank != null && EF.Functions.Like(r.ToBank.ToLower(), like)) ||
                            (r.BeneficiaryName != null && EF.Functions.Like(r.BeneficiaryName.ToLower(), like)) ||
                            (r.PurposeOfTransfer != null && EF.Functions.Like(r.PurposeOfTransfer.ToLower(), like)) ||
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

        public async Task<int> GetCountByUserAsync(int userId, string? searchTerm, string? searchBy)
        {
            var q = _context.ForeignTransferRequests
                .Include(r => r.Company)
                .Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "tobank":
                        q = q.Where(r => r.ToBank != null &&
                                         EF.Functions.Like(r.ToBank.ToLower(), like));
                        break;

                    case "beneficiary":
                        q = q.Where(r => r.BeneficiaryName != null &&
                                         EF.Functions.Like(r.BeneficiaryName.ToLower(), like));
                        break;

                    case "code":
                        q = q.Where(r => r.Company != null && r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.ToBank != null && EF.Functions.Like(r.ToBank.ToLower(), like)) ||
                            (r.BeneficiaryName != null && EF.Functions.Like(r.BeneficiaryName.ToLower(), like)) ||
                            (r.PurposeOfTransfer != null && EF.Functions.Like(r.PurposeOfTransfer.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Company != null && r.Company.Name != null && EF.Functions.Like(r.Company.Name.ToLower(), like)));
                        break;
                }
            }

            return await q.AsNoTracking().CountAsync();
        }

        // ---------------- ADMIN: all requests ----------------
        public async Task<IList<ForeignTransfer>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.ForeignTransferRequests
                .Include(r => r.Company)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "tobank":
                        q = q.Where(r => r.ToBank != null &&
                                         EF.Functions.Like(r.ToBank.ToLower(), like));
                        break;

                    case "beneficiary":
                        q = q.Where(r => r.BeneficiaryName != null &&
                                         EF.Functions.Like(r.BeneficiaryName.ToLower(), like));
                        break;

                    case "code":
                        q = q.Where(r => r.Company != null && r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.ToBank != null && EF.Functions.Like(r.ToBank.ToLower(), like)) ||
                            (r.BeneficiaryName != null && EF.Functions.Like(r.BeneficiaryName.ToLower(), like)) ||
                            (r.PurposeOfTransfer != null && EF.Functions.Like(r.PurposeOfTransfer.ToLower(), like)) ||
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
            var q = _context.ForeignTransferRequests
                .Include(r => r.Company)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "tobank":
                        q = q.Where(r => r.ToBank != null &&
                                         EF.Functions.Like(r.ToBank.ToLower(), like));
                        break;

                    case "beneficiary":
                        q = q.Where(r => r.BeneficiaryName != null &&
                                         EF.Functions.Like(r.BeneficiaryName.ToLower(), like));
                        break;

                    case "code":
                        q = q.Where(r => r.Company != null && r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.ToBank != null && EF.Functions.Like(r.ToBank.ToLower(), like)) ||
                            (r.BeneficiaryName != null && EF.Functions.Like(r.BeneficiaryName.ToLower(), like)) ||
                            (r.PurposeOfTransfer != null && EF.Functions.Like(r.PurposeOfTransfer.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Company != null && r.Company.Name != null && EF.Functions.Like(r.Company.Name.ToLower(), like)));
                        break;
                }
            }

            return await q.AsNoTracking().CountAsync();
        }

        // ---------------- COMPANY: by company ----------------
        public async Task<IList<ForeignTransfer>> GetAllByCompanyAsync(
            int companyId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.ForeignTransferRequests
                .Include(r => r.Company)
                .Where(r => r.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "status":
                        q = q.Where(r => r.Status != null &&
                                         EF.Functions.Like(r.Status.ToLower(), like));
                        break;

                    case "beneficiary":
                        q = q.Where(r => r.BeneficiaryName != null &&
                                         EF.Functions.Like(r.BeneficiaryName.ToLower(), like));
                        break;

                    case "code": // supported, though companyId already filters company
                        q = q.Where(r => r.Company != null && r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.BeneficiaryName != null && EF.Functions.Like(r.BeneficiaryName.ToLower(), like)) ||
                            (r.Status != null && EF.Functions.Like(r.Status.ToLower(), like)) ||
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

        public async Task<int> GetCountByCompanyAsync(int companyId, string? searchTerm, string? searchBy)
        {
            var q = _context.ForeignTransferRequests
                .Include(r => r.Company)
                .Where(r => r.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "status":
                        q = q.Where(r => r.Status != null &&
                                         EF.Functions.Like(r.Status.ToLower(), like));
                        break;

                    case "beneficiary":
                        q = q.Where(r => r.BeneficiaryName != null &&
                                         EF.Functions.Like(r.BeneficiaryName.ToLower(), like));
                        break;

                    case "code":
                        q = q.Where(r => r.Company != null && r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.BeneficiaryName != null && EF.Functions.Like(r.BeneficiaryName.ToLower(), like)) ||
                            (r.Status != null && EF.Functions.Like(r.Status.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Company != null && r.Company.Name != null && EF.Functions.Like(r.Company.Name.ToLower(), like)));
                        break;
                }
            }

            return await q.AsNoTracking().CountAsync();
        }

        public async Task<ForeignTransfer?> GetByIdAsync(int id)
            => await _context.ForeignTransferRequests
                .Include(r => r.Company)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

        public async Task UpdateAsync(ForeignTransfer req)
        {
            _context.ForeignTransferRequests.Update(req);
            await _context.SaveChangesAsync();
        }
    }
}
