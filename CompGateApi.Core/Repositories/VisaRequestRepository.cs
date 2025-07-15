// ── CompGateApi.Data.Repositories/VisaRequestRepository.cs ──────────────
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class VisaRequestRepository : IVisaRequestRepository
    {
        private readonly CompGateApiDbContext _context;
        public VisaRequestRepository(CompGateApiDbContext context) => _context = context;

        public async Task CreateAsync(VisaRequest entity)
        {
            _context.VisaRequests.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _context.VisaRequests.FindAsync(id);
            if (ent != null)
            {
                _context.VisaRequests.Remove(ent);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IList<VisaRequest>> GetAllByUserAsync(int userId, string? searchTerm, int page, int limit)
        {
            var q = _context.VisaRequests.Where(v => v.UserId == userId);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                q = q.Where(v =>
                    v.AccountHolderName!.Contains(searchTerm) ||
                    v.AccountNumber!.Contains(searchTerm) ||
                    v.Cbl!.Contains(searchTerm));
            }
            return await q
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetCountByUserAsync(int userId, string? searchTerm)
        {
            var q = _context.VisaRequests.Where(v => v.UserId == userId);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                q = q.Where(v =>
                    v.AccountHolderName!.Contains(searchTerm) ||
                    v.AccountNumber!.Contains(searchTerm) ||
                    v.Cbl!.Contains(searchTerm));
            }
            return await q.CountAsync();
        }

        public async Task<IList<VisaRequest>> GetAllAsync(string? searchTerm, int page, int limit)
        {
            var q = _context.VisaRequests.AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                q = q.Where(v =>
                    v.AccountHolderName!.Contains(searchTerm) ||
                    v.AccountNumber!.Contains(searchTerm) ||
                    v.Cbl!.Contains(searchTerm));
            }
            return await q
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetCountAsync(string? searchTerm)
        {
            var q = _context.VisaRequests.AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                q = q.Where(v =>
                    v.AccountHolderName!.Contains(searchTerm) ||
                    v.AccountNumber!.Contains(searchTerm) ||
                    v.Cbl!.Contains(searchTerm));
            }
            return await q.CountAsync();
        }

        public async Task<VisaRequest?> GetByIdAsync(int id)
            => await _context.VisaRequests
                .AsNoTracking()
                .Include(v => v.Attachments)
                .FirstOrDefaultAsync(v => v.Id == id);

        public async Task UpdateAsync(VisaRequest entity)
        {
            var existing = await _context.VisaRequests
                .FirstOrDefaultAsync(v => v.Id == entity.Id);

            if (existing == null)
                throw new KeyNotFoundException($"VisaRequest {entity.Id} not found.");

            // copy only scalar fields (not attachments)
            existing.Branch = entity.Branch;
            existing.Date = entity.Date;
            existing.AccountHolderName = entity.AccountHolderName;
            existing.AccountNumber = entity.AccountNumber;
            existing.NationalId = entity.NationalId;
            existing.PhoneNumberLinkedToNationalId = entity.PhoneNumberLinkedToNationalId;
            existing.Cbl = entity.Cbl;
            existing.CardMovementApproval = entity.CardMovementApproval;
            existing.CardUsingAcknowledgment = entity.CardUsingAcknowledgment;
            existing.ForeignAmount = entity.ForeignAmount;
            existing.LocalAmount = entity.LocalAmount;
            existing.Pldedge = entity.Pldedge;
            // leave Status & Reason unchanged

            await _context.SaveChangesAsync();
        }

        public async Task<IList<VisaRequest>> GetAllByCompanyAsync(
      int companyId,               // renamed parameter
      string? searchTerm,
      string? searchBy,
      int page,
      int limit)
        {
            var q = _context.VisaRequests
                .Where(v => v.CompanyId == companyId);  // ← filter on CompanyId

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "accountholder":
                        q = q.Where(v => v.AccountHolderName!.Contains(searchTerm));
                        break;
                    case "accountnumber":
                        q = q.Where(v => v.AccountNumber!.Contains(searchTerm));
                        break;
                    case "cbl":
                        q = q.Where(v => v.Cbl!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(v =>
                            v.AccountHolderName!.Contains(searchTerm) ||
                            v.AccountNumber!.Contains(searchTerm) ||
                            v.Cbl!.Contains(searchTerm));
                        break;
                }
            }

            return await q
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetCountByCompanyAsync(
    int companyId,               // renamed parameter
    string? searchTerm,
    string? searchBy)
        {
            var q = _context.VisaRequests
                .Where(v => v.CompanyId == companyId);  // ← filter on CompanyId

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "accountholder":
                        q = q.Where(v => v.AccountHolderName!.Contains(searchTerm));
                        break;
                    case "accountnumber":
                        q = q.Where(v => v.AccountNumber!.Contains(searchTerm));
                        break;
                    case "cbl":
                        q = q.Where(v => v.Cbl!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(v =>
                            v.AccountHolderName!.Contains(searchTerm) ||
                            v.AccountNumber!.Contains(searchTerm) ||
                            v.Cbl!.Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }
    }
}
