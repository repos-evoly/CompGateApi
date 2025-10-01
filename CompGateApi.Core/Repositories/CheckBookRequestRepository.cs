// CompGateApi.Data.Repositories/CheckBookRequestRepository.cs
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

        // ---------------- COMPANY: by user ----------------
        public async Task<IList<CheckBookRequest>> GetAllByUserAsync(
            int userId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CheckBookRequests
                            .Include(r => r.Company)
                            .Include(r => r.Representative)
                            .Where(r => r.UserId == userId);

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

                    case "branch":
                        q = q.Where(r => r.Branch != null &&
                                         EF.Functions.Like(r.Branch.ToLower(), like));
                        break;

                    case "code":
                        q = q.Where(r => r.Company != null &&
                                         r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    case "rep":
                    case "representative":
                    case "representativename":
                        q = q.Where(r => r.Representative != null &&
                                         r.Representative.Name != null &&
                                         EF.Functions.Like(r.Representative.Name.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.FullName != null && EF.Functions.Like(r.FullName.ToLower(), like)) ||
                            (r.AccountNumber != null && EF.Functions.Like(r.AccountNumber.ToLower(), like)) ||
                            (r.Branch != null && EF.Functions.Like(r.Branch.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Representative != null && r.Representative.Name != null && EF.Functions.Like(r.Representative.Name.ToLower(), like)));
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
            var q = _context.CheckBookRequests
                            .Include(r => r.Company)
                            .Include(r => r.Representative)
                            .Where(r => r.UserId == userId);

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

                    case "branch":
                        q = q.Where(r => r.Branch != null &&
                                         EF.Functions.Like(r.Branch.ToLower(), like));
                        break;

                    case "code":
                        q = q.Where(r => r.Company != null &&
                                         r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    case "rep":
                    case "representative":
                    case "representativename":
                        q = q.Where(r => r.Representative != null &&
                                         r.Representative.Name != null &&
                                         EF.Functions.Like(r.Representative.Name.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.FullName != null && EF.Functions.Like(r.FullName.ToLower(), like)) ||
                            (r.AccountNumber != null && EF.Functions.Like(r.AccountNumber.ToLower(), like)) ||
                            (r.Branch != null && EF.Functions.Like(r.Branch.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Representative != null && r.Representative.Name != null && EF.Functions.Like(r.Representative.Name.ToLower(), like)));
                        break;
                }
            }

            return await q.CountAsync();
        }

        // ---------------- ADMIN: all requests ----------------
        public async Task<IList<CheckBookRequest>> GetAllAsync(
            string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CheckBookRequests
                            .Include(r => r.Company)
                            .Include(r => r.Representative)
                            .AsQueryable();

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

                    case "branch":
                        q = q.Where(r => r.Branch != null &&
                                         EF.Functions.Like(r.Branch.ToLower(), like));
                        break;

                    case "code":
                        q = q.Where(r => r.Company != null &&
                                         r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    case "rep":
                    case "representative":
                    case "representativename":
                        q = q.Where(r => r.Representative != null &&
                                         r.Representative.Name != null &&
                                         EF.Functions.Like(r.Representative.Name.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.FullName != null && EF.Functions.Like(r.FullName.ToLower(), like)) ||
                            (r.AccountNumber != null && EF.Functions.Like(r.AccountNumber.ToLower(), like)) ||
                            (r.Branch != null && EF.Functions.Like(r.Branch.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Representative != null && r.Representative.Name != null && EF.Functions.Like(r.Representative.Name.ToLower(), like)));
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
            var q = _context.CheckBookRequests
                            .Include(r => r.Company)
                            .Include(r => r.Representative)
                            .AsQueryable();

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

                    case "branch":
                        q = q.Where(r => r.Branch != null &&
                                         EF.Functions.Like(r.Branch.ToLower(), like));
                        break;

                    case "code":
                        q = q.Where(r => r.Company != null &&
                                         r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    case "rep":
                    case "representative":
                    case "representativename":
                        q = q.Where(r => r.Representative != null &&
                                         r.Representative.Name != null &&
                                         EF.Functions.Like(r.Representative.Name.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.FullName != null && EF.Functions.Like(r.FullName.ToLower(), like)) ||
                            (r.AccountNumber != null && EF.Functions.Like(r.AccountNumber.ToLower(), like)) ||
                            (r.Branch != null && EF.Functions.Like(r.Branch.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Representative != null && r.Representative.Name != null && EF.Functions.Like(r.Representative.Name.ToLower(), like)));
                        break;
                }
            }

            return await q.AsNoTracking().CountAsync();
        }

        public async Task<CheckBookRequest?> GetByIdAsync(int id)
            => await _context.CheckBookRequests
                .Include(r => r.Company)
                .Include(r => r.Representative)
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

        // ---------------- COMPANY: by company ----------------
        public async Task<IList<CheckBookRequest>> GetAllByCompanyAsync(
            int companyId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CheckBookRequests
                            .Include(r => r.Company)
                            .Include(r => r.Representative)
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

                    case "branch":
                        q = q.Where(r => r.Branch != null &&
                                         EF.Functions.Like(r.Branch.ToLower(), like));
                        break;

                    case "code":
                        q = q.Where(r => r.Company != null &&
                                         r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    case "rep":
                    case "representative":
                    case "representativename":
                        q = q.Where(r => r.Representative != null &&
                                         r.Representative.Name != null &&
                                         EF.Functions.Like(r.Representative.Name.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.FullName != null && EF.Functions.Like(r.FullName.ToLower(), like)) ||
                            (r.AccountNumber != null && EF.Functions.Like(r.AccountNumber.ToLower(), like)) ||
                            (r.Branch != null && EF.Functions.Like(r.Branch.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Representative != null && r.Representative.Name != null && EF.Functions.Like(r.Representative.Name.ToLower(), like)));
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
            var q = _context.CheckBookRequests
                            .Include(r => r.Company)
                            .Include(r => r.Representative)
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

                    case "branch":
                        q = q.Where(r => r.Branch != null &&
                                         EF.Functions.Like(r.Branch.ToLower(), like));
                        break;

                    case "code":
                        q = q.Where(r => r.Company != null &&
                                         r.Company.Code != null &&
                                         EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;

                    case "rep":
                    case "representative":
                    case "representativename":
                        q = q.Where(r => r.Representative != null &&
                                         r.Representative.Name != null &&
                                         EF.Functions.Like(r.Representative.Name.ToLower(), like));
                        break;

                    default:
                        q = q.Where(r =>
                            (r.FullName != null && EF.Functions.Like(r.FullName.ToLower(), like)) ||
                            (r.AccountNumber != null && EF.Functions.Like(r.AccountNumber.ToLower(), like)) ||
                            (r.Branch != null && EF.Functions.Like(r.Branch.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Representative != null && r.Representative.Name != null && EF.Functions.Like(r.Representative.Name.ToLower(), like)));
                        break;
                }
            }

            return await q.CountAsync();
        }
    }
}
