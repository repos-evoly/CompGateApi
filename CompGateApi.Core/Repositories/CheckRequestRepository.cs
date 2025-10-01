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

        // ---------------- COMPANY: by user ----------------
        public async Task<IList<CheckRequest>> GetAllByUserAsync(
            int userId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CheckRequests
                .Include(r => r.LineItems)
                .Include(r => r.Company)
                .Include(r => r.Representative)
                .Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "customer":
                        q = q.Where(r => r.CustomerName != null &&
                                         EF.Functions.Like(r.CustomerName.ToLower(), like));
                        break;

                    case "status":
                        q = q.Where(r => r.Status != null &&
                                         EF.Functions.Like(r.Status.ToLower(), like));
                        break;

                    case "rep":
                    case "representative":
                        q = q.Where(r => r.Representative != null &&
                                         r.Representative.Name != null &&
                                         EF.Functions.Like(r.Representative.Name.ToLower(), like));
                        break;

                    case "code": // if you want code filter here too
                        q = q.Where(r => r.Company != null &&
                                         r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.CustomerName != null && EF.Functions.Like(r.CustomerName.ToLower(), like)) ||
                            (r.AccountNum != null && EF.Functions.Like(r.AccountNum.ToLower(), like)) ||
                            (r.Beneficiary != null && EF.Functions.Like(r.Beneficiary.ToLower(), like)) ||
                            (r.Representative != null && r.Representative.Name != null &&
                                EF.Functions.Like(r.Representative.Name.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null &&
                                EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Company != null && r.Company.Name != null &&
                                EF.Functions.Like(r.Company.Name.ToLower(), like)));
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
            var q = _context.CheckRequests
                .Include(r => r.Company)
                .Include(r => r.Representative)
                .Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "customer":
                        q = q.Where(r => r.CustomerName != null &&
                                         EF.Functions.Like(r.CustomerName.ToLower(), like));
                        break;

                    case "status":
                        q = q.Where(r => r.Status != null &&
                                         EF.Functions.Like(r.Status.ToLower(), like));
                        break;

                    case "rep":
                    case "representative":
                        q = q.Where(r => r.Representative != null &&
                                         r.Representative.Name != null &&
                                         EF.Functions.Like(r.Representative.Name.ToLower(), like));
                        break;

                    case "code":
                        q = q.Where(r => r.Company != null &&
                                         r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.CustomerName != null && EF.Functions.Like(r.CustomerName.ToLower(), like)) ||
                            (r.AccountNum != null && EF.Functions.Like(r.AccountNum.ToLower(), like)) ||
                            (r.Beneficiary != null && EF.Functions.Like(r.Beneficiary.ToLower(), like)) ||
                            (r.Representative != null && r.Representative.Name != null &&
                                EF.Functions.Like(r.Representative.Name.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null &&
                                EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Company != null && r.Company.Name != null &&
                                EF.Functions.Like(r.Company.Name.ToLower(), like)));
                        break;
                }
            }

            return await q.CountAsync();
        }

        // ---------------- ADMIN: all requests ----------------
        public async Task<IList<CheckRequest>> GetAllAsync(
            string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CheckRequests
                .Include(r => r.LineItems)
                .Include(r => r.Company)
                .Include(r => r.Representative)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "customer":
                        q = q.Where(r => r.CustomerName != null &&
                                         EF.Functions.Like(r.CustomerName.ToLower(), like));
                        break;

                    case "status":
                        q = q.Where(r => r.Status != null &&
                                         EF.Functions.Like(r.Status.ToLower(), like));
                        break;

                    case "code":
                        q = q.Where(r => r.Company != null &&
                                         r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    case "rep":
                    case "representative":
                        q = q.Where(r => r.Representative != null &&
                                         r.Representative.Name != null &&
                                         EF.Functions.Like(r.Representative.Name.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.CustomerName != null && EF.Functions.Like(r.CustomerName.ToLower(), like)) ||
                            (r.AccountNum != null && EF.Functions.Like(r.AccountNum.ToLower(), like)) ||
                            (r.Beneficiary != null && EF.Functions.Like(r.Beneficiary.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null &&
                                EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Company != null && r.Company.Name != null &&
                                EF.Functions.Like(r.Company.Name.ToLower(), like)) ||
                            (r.Representative != null && r.Representative.Name != null &&
                                EF.Functions.Like(r.Representative.Name.ToLower(), like)));
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
            var q = _context.CheckRequests
                .Include(r => r.Company)
                .Include(r => r.Representative)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "customer":
                        q = q.Where(r => r.CustomerName != null &&
                                         EF.Functions.Like(r.CustomerName.ToLower(), like));
                        break;

                    case "status":
                        q = q.Where(r => r.Status != null &&
                                         EF.Functions.Like(r.Status.ToLower(), like));
                        break;

                    case "code":
                        q = q.Where(r => r.Company != null &&
                                         r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    case "rep":
                    case "representative":
                        q = q.Where(r => r.Representative != null &&
                                         r.Representative.Name != null &&
                                         EF.Functions.Like(r.Representative.Name.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.CustomerName != null && EF.Functions.Like(r.CustomerName.ToLower(), like)) ||
                            (r.AccountNum != null && EF.Functions.Like(r.AccountNum.ToLower(), like)) ||
                            (r.Beneficiary != null && EF.Functions.Like(r.Beneficiary.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null &&
                                EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Company != null && r.Company.Name != null &&
                                EF.Functions.Like(r.Company.Name.ToLower(), like)) ||
                            (r.Representative != null && r.Representative.Name != null &&
                                EF.Functions.Like(r.Representative.Name.ToLower(), like)));
                        break;
                }
            }

            return await q.AsNoTracking().CountAsync();
        }

        public async Task<CheckRequest?> GetByIdAsync(int id)
            => await _context.CheckRequests
                .Include(r => r.LineItems)
                .Include(r => r.Company)
                .Include(r => r.Representative)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

        public async Task UpdateAsync(CheckRequest req)
        {
            _context.CheckRequests.Update(req);
            await _context.SaveChangesAsync();
        }

        // ---------------- COMPANY: by company ----------------
        public async Task<IList<CheckRequest>> GetAllByCompanyAsync(
            int companyId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CheckRequests
                .Include(r => r.LineItems)
                .Include(r => r.Company)
                .Include(r => r.Representative)
                .Where(r => r.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "customer":
                        q = q.Where(r => r.CustomerName != null &&
                                         EF.Functions.Like(r.CustomerName.ToLower(), like));
                        break;

                    case "status":
                        q = q.Where(r => r.Status != null &&
                                         EF.Functions.Like(r.Status.ToLower(), like));
                        break;

                    case "rep":
                    case "representative":
                        q = q.Where(r => r.Representative != null &&
                                         r.Representative.Name != null &&
                                         EF.Functions.Like(r.Representative.Name.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.CustomerName != null && EF.Functions.Like(r.CustomerName.ToLower(), like)) ||
                            (r.AccountNum != null && EF.Functions.Like(r.AccountNum.ToLower(), like)) ||
                            (r.Beneficiary != null && EF.Functions.Like(r.Beneficiary.ToLower(), like)) ||
                            (r.Representative != null && r.Representative.Name != null &&
                                EF.Functions.Like(r.Representative.Name.ToLower(), like)));
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
            var q = _context.CheckRequests
                .Include(r => r.Company)
                .Include(r => r.Representative)
                .Where(r => r.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "customer":
                        q = q.Where(r => r.CustomerName != null &&
                                         EF.Functions.Like(r.CustomerName.ToLower(), like));
                        break;

                    case "status":
                        q = q.Where(r => r.Status != null &&
                                         EF.Functions.Like(r.Status.ToLower(), like));
                        break;

                    case "rep":
                    case "representative":
                        q = q.Where(r => r.Representative != null &&
                                         r.Representative.Name != null &&
                                         EF.Functions.Like(r.Representative.Name.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.CustomerName != null && EF.Functions.Like(r.CustomerName.ToLower(), like)) ||
                            (r.AccountNum != null && EF.Functions.Like(r.AccountNum.ToLower(), like)) ||
                            (r.Beneficiary != null && EF.Functions.Like(r.Beneficiary.ToLower(), like)) ||
                            (r.Representative != null && r.Representative.Name != null &&
                                EF.Functions.Like(r.Representative.Name.ToLower(), like)));
                        break;
                }
            }

            return await q.CountAsync();
        }
    }
}
