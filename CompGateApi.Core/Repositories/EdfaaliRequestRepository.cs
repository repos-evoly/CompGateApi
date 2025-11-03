using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class EdfaaliRequestRepository : IEdfaaliRequestRepository
    {
        private readonly CompGateApiDbContext _context;
        public EdfaaliRequestRepository(CompGateApiDbContext context) => _context = context;

        public async Task CreateAsync(EdfaaliRequest entity)
        {
            _context.EdfaaliRequests.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(EdfaaliRequest entity)
        {
            _context.EdfaaliRequests.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _context.EdfaaliRequests.FindAsync(id);
            if (ent != null)
            {
                _context.EdfaaliRequests.Remove(ent);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<EdfaaliRequest?> GetByIdAsync(int id)
            => await _context.EdfaaliRequests
                .Include(r => r.Attachments)
                .Include(r => r.Representative)
                .Include(r => r.Company)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

        // COMPANY
        public async Task<IList<EdfaaliRequest>> GetAllByCompanyAsync(int companyId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.EdfaaliRequests
                .Include(r => r.Attachments)
                .Include(r => r.Representative)
                .Where(r => r.CompanyId == companyId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                switch ((searchBy ?? "").ToLower())
                {
                    case "rep":
                    case "representative":
                        q = q.Where(r => r.Representative != null && r.Representative.Name != null && r.Representative.Name.ToLower().Contains(term));
                        break;
                    case "company":
                        q = q.Where(r => r.CompanyEnglishName != null && r.CompanyEnglishName.ToLower().Contains(term));
                        break;
                    case "account":
                        q = q.Where(r => r.AccountNumber != null && r.AccountNumber.ToLower().Contains(term));
                        break;
                    default:
                        q = q.Where(r =>
                            (r.CompanyEnglishName != null && r.CompanyEnglishName.ToLower().Contains(term)) ||
                            (r.AccountNumber != null && r.AccountNumber.ToLower().Contains(term)) ||
                            (r.City != null && r.City.ToLower().Contains(term)) ||
                            (r.Area != null && r.Area.ToLower().Contains(term)));
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
            var q = _context.EdfaaliRequests.Where(r => r.CompanyId == companyId).AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                switch ((searchBy ?? "").ToLower())
                {
                    case "rep":
                    case "representative":
                        q = q.Where(r => r.Representative != null && r.Representative.Name != null && r.Representative.Name.ToLower().Contains(term));
                        break;
                    case "company":
                        q = q.Where(r => r.CompanyEnglishName != null && r.CompanyEnglishName.ToLower().Contains(term));
                        break;
                    case "account":
                        q = q.Where(r => r.AccountNumber != null && r.AccountNumber.ToLower().Contains(term));
                        break;
                    default:
                        q = q.Where(r =>
                            (r.CompanyEnglishName != null && r.CompanyEnglishName.ToLower().Contains(term)) ||
                            (r.AccountNumber != null && r.AccountNumber.ToLower().Contains(term)) ||
                            (r.City != null && r.City.ToLower().Contains(term)) ||
                            (r.Area != null && r.Area.ToLower().Contains(term)));
                        break;
                }
            }

            return await q.CountAsync();
        }

        // ADMIN
        public async Task<IList<EdfaaliRequest>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.EdfaaliRequests
                .Include(r => r.Company)
                .Include(r => r.Representative)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";
                switch ((searchBy ?? "").ToLower())
                {
                    case "code":
                    case "companycode":
                        q = q.Where(r => r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;
                    case "company":
                    case "companyname":
                        q = q.Where(r => r.Company != null && r.Company.Name != null && EF.Functions.Like(r.Company.Name.ToLower(), like));
                        break;
                    case "rep":
                    case "representative":
                        q = q.Where(r => r.Representative != null && r.Representative.Name != null && EF.Functions.Like(r.Representative.Name.ToLower(), like));
                        break;
                    default:
                        q = q.Where(r =>
                            (r.CompanyEnglishName != null && EF.Functions.Like(r.CompanyEnglishName.ToLower(), like)) ||
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
            var q = _context.EdfaaliRequests
                .Include(r => r.Company)
                .Include(r => r.Representative)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";
                switch ((searchBy ?? "").ToLower())
                {
                    case "code":
                    case "companycode":
                        q = q.Where(r => r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like));
                        break;
                    case "company":
                    case "companyname":
                        q = q.Where(r => r.Company != null && r.Company.Name != null && EF.Functions.Like(r.Company.Name.ToLower(), like));
                        break;
                    case "rep":
                    case "representative":
                        q = q.Where(r => r.Representative != null && r.Representative.Name != null && EF.Functions.Like(r.Representative.Name.ToLower(), like));
                        break;
                    default:
                        q = q.Where(r =>
                            (r.CompanyEnglishName != null && EF.Functions.Like(r.CompanyEnglishName.ToLower(), like)) ||
                            (r.AccountNumber != null && EF.Functions.Like(r.AccountNumber.ToLower(), like)) ||
                            (r.Company != null && r.Company.Code != null && EF.Functions.Like(r.Company.Code.ToLower(), like)) ||
                            (r.Company != null && r.Company.Name != null && EF.Functions.Like(r.Company.Name.ToLower(), like)));
                        break;
                }
            }

            return await q.AsNoTracking().CountAsync();
        }
    }
}

