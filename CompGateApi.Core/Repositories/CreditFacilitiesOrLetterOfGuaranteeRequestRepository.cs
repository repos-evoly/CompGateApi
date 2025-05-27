using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class CreditFacilitiesOrLetterOfGuaranteeRequestRepository
        : ICreditFacilitiesOrLetterOfGuaranteeRequestRepository
    {
        private readonly CompGateApiDbContext _context;
        public CreditFacilitiesOrLetterOfGuaranteeRequestRepository(CompGateApiDbContext context)
            => _context = context;

        public async Task<IList<CreditFacilitiesOrLetterOfGuaranteeRequest>> GetAllByUserAsync(
            int userId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CreditFacilitiesOrLetterOfGuaranteeRequests
                            .Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "accountnumber":
                        q = q.Where(r => r.AccountNumber.Contains(searchTerm));
                        break;
                    case "type":
                        q = q.Where(r => r.Type.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.Purpose.Contains(searchTerm) ||
                            r.Status.Contains(searchTerm) ||
                            r.ReferenceNumber.Contains(searchTerm));
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
            var q = _context.CreditFacilitiesOrLetterOfGuaranteeRequests
                            .Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "accountnumber":
                        q = q.Where(r => r.AccountNumber.Contains(searchTerm));
                        break;
                    case "type":
                        q = q.Where(r => r.Type.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.Purpose.Contains(searchTerm) ||
                            r.Status.Contains(searchTerm) ||
                            r.ReferenceNumber.Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }

        public async Task<IList<CreditFacilitiesOrLetterOfGuaranteeRequest>> GetAllAsync(
            string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.CreditFacilitiesOrLetterOfGuaranteeRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "accountnumber":
                        q = q.Where(r => r.AccountNumber.Contains(searchTerm));
                        break;
                    case "type":
                        q = q.Where(r => r.Type.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.Purpose.Contains(searchTerm) ||
                            r.Status.Contains(searchTerm) ||
                            r.ReferenceNumber.Contains(searchTerm));
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
            var q = _context.CreditFacilitiesOrLetterOfGuaranteeRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "accountnumber":
                        q = q.Where(r => r.AccountNumber.Contains(searchTerm));
                        break;
                    case "type":
                        q = q.Where(r => r.Type.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.Purpose.Contains(searchTerm) ||
                            r.Status.Contains(searchTerm) ||
                            r.ReferenceNumber.Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }

        public async Task<CreditFacilitiesOrLetterOfGuaranteeRequest?> GetByIdAsync(int id)
            => await _context.CreditFacilitiesOrLetterOfGuaranteeRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

        public async Task CreateAsync(CreditFacilitiesOrLetterOfGuaranteeRequest entity)
        {
            _context.CreditFacilitiesOrLetterOfGuaranteeRequests.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(CreditFacilitiesOrLetterOfGuaranteeRequest entity)
        {
            _context.CreditFacilitiesOrLetterOfGuaranteeRequests.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _context.CreditFacilitiesOrLetterOfGuaranteeRequests.FindAsync(id);
            if (ent != null)
            {
                _context.CreditFacilitiesOrLetterOfGuaranteeRequests.Remove(ent);
                await _context.SaveChangesAsync();
            }
        }
    }
}
