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

        // ---------------- COMPANY USER ----------------
        public async Task<IList<VisaRequest>> GetAllByUserAsync(
            int userId, string? searchTerm, int page, int limit)
        {
            var q = _context.VisaRequests
                            .Include(v => v.Company)
                            .Where(v => v.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                q = q.Where(v =>
                    (v.AccountHolderName != null && EF.Functions.Like(v.AccountHolderName.ToLower(), like)) ||
                    (v.AccountNumber != null && EF.Functions.Like(v.AccountNumber.ToLower(), like)) ||
                    (v.Cbl != null && EF.Functions.Like(v.Cbl.ToLower(), like)) ||
                    (v.Company != null && v.Company.Code != null && EF.Functions.Like(v.Company.Code.ToLower(), like)) ||
                    (v.Company != null && v.Company.Name != null && EF.Functions.Like(v.Company.Name.ToLower(), like))
                );
            }

            return await q.OrderByDescending(v => v.CreatedAt)
                          .Skip((page - 1) * limit)
                          .Take(limit)
                          .AsNoTracking()
                          .ToListAsync();
        }

        public async Task<int> GetCountByUserAsync(int userId, string? searchTerm)
        {
            var q = _context.VisaRequests
                            .Include(v => v.Company)
                            .Where(v => v.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                q = q.Where(v =>
                    (v.AccountHolderName != null && EF.Functions.Like(v.AccountHolderName.ToLower(), like)) ||
                    (v.AccountNumber != null && EF.Functions.Like(v.AccountNumber.ToLower(), like)) ||
                    (v.Cbl != null && EF.Functions.Like(v.Cbl.ToLower(), like)) ||
                    (v.Company != null && v.Company.Code != null && EF.Functions.Like(v.Company.Code.ToLower(), like)) ||
                    (v.Company != null && v.Company.Name != null && EF.Functions.Like(v.Company.Name.ToLower(), like))
                );
            }

            return await q.AsNoTracking().CountAsync();
        }

        // ---------------- ADMIN (all) ----------------
        public async Task<IList<VisaRequest>> GetAllAsync(
            string? searchTerm, int page, int limit)
        {
            var q = _context.VisaRequests
                            .Include(v => v.Company)
                            .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                q = q.Where(v =>
                    (v.AccountHolderName != null && EF.Functions.Like(v.AccountHolderName.ToLower(), like)) ||
                    (v.AccountNumber != null && EF.Functions.Like(v.AccountNumber.ToLower(), like)) ||
                    (v.Cbl != null && EF.Functions.Like(v.Cbl.ToLower(), like)) ||
                    (v.Company != null && v.Company.Code != null && EF.Functions.Like(v.Company.Code.ToLower(), like)) ||
                    (v.Company != null && v.Company.Name != null && EF.Functions.Like(v.Company.Name.ToLower(), like))
                );
            }

            return await q.OrderByDescending(v => v.CreatedAt)
                          .Skip((page - 1) * limit)
                          .Take(limit)
                          .AsNoTracking()
                          .ToListAsync();
        }

        public async Task<int> GetCountAsync(string? searchTerm)
        {
            var q = _context.VisaRequests
                            .Include(v => v.Company)
                            .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                q = q.Where(v =>
                    (v.AccountHolderName != null && EF.Functions.Like(v.AccountHolderName.ToLower(), like)) ||
                    (v.AccountNumber != null && EF.Functions.Like(v.AccountNumber.ToLower(), like)) ||
                    (v.Cbl != null && EF.Functions.Like(v.Cbl.ToLower(), like)) ||
                    (v.Company != null && v.Company.Code != null && EF.Functions.Like(v.Company.Code.ToLower(), like)) ||
                    (v.Company != null && v.Company.Name != null && EF.Functions.Like(v.Company.Name.ToLower(), like))
                );
            }

            return await q.AsNoTracking().CountAsync();
        }

        // ---------------- COMPANY (by company) ----------------
        public async Task<IList<VisaRequest>> GetAllByCompanyAsync(
            int companyId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.VisaRequests
                            .Include(v => v.Company)
                            .Where(v => v.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "accountholder":
                        q = q.Where(v => v.AccountHolderName != null &&
                                         EF.Functions.Like(v.AccountHolderName.ToLower(), like));
                        break;

                    case "accountnumber":
                        q = q.Where(v => v.AccountNumber != null &&
                                         EF.Functions.Like(v.AccountNumber.ToLower(), like));
                        break;

                    case "cbl":
                        q = q.Where(v => v.Cbl != null &&
                                         EF.Functions.Like(v.Cbl.ToLower(), like));
                        break;

                    case "code": // supported, though scoped to a single company
                        q = q.Where(v => v.Company != null && v.Company.Code != null &&
                                         EF.Functions.Like(v.Company.Code.ToLower(), like));
                        break;

                    default:
                        q = q.Where(v =>
                            (v.AccountHolderName != null && EF.Functions.Like(v.AccountHolderName.ToLower(), like)) ||
                            (v.AccountNumber != null && EF.Functions.Like(v.AccountNumber.ToLower(), like)) ||
                            (v.Cbl != null && EF.Functions.Like(v.Cbl.ToLower(), like)) ||
                            (v.Company != null && v.Company.Name != null && EF.Functions.Like(v.Company.Name.ToLower(), like))
                        );
                        break;
                }
            }

            return await q.OrderByDescending(v => v.CreatedAt)
                          .Skip((page - 1) * limit)
                          .Take(limit)
                          .AsNoTracking()
                          .ToListAsync();
        }

        public async Task<int> GetCountByCompanyAsync(
            int companyId, string? searchTerm, string? searchBy)
        {
            var q = _context.VisaRequests
                            .Include(v => v.Company)
                            .Where(v => v.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var like = $"%{term}%";

                switch ((searchBy ?? "").ToLower())
                {
                    case "accountholder":
                        q = q.Where(v => v.AccountHolderName != null &&
                                         EF.Functions.Like(v.AccountHolderName.ToLower(), like));
                        break;

                    case "accountnumber":
                        q = q.Where(v => v.AccountNumber != null &&
                                         EF.Functions.Like(v.AccountNumber.ToLower(), like));
                        break;

                    case "cbl":
                        q = q.Where(v => v.Cbl != null &&
                                         EF.Functions.Like(v.Cbl.ToLower(), like));
                        break;

                    case "code":
                        q = q.Where(v => v.Company != null && v.Company.Code != null &&
                                         EF.Functions.Like(v.Company.Code.ToLower(), like));
                        break;

                    default:
                        q = q.Where(v =>
                            (v.AccountHolderName != null && EF.Functions.Like(v.AccountHolderName.ToLower(), like)) ||
                            (v.AccountNumber != null && EF.Functions.Like(v.AccountNumber.ToLower(), like)) ||
                            (v.Cbl != null && EF.Functions.Like(v.Cbl.ToLower(), like)) ||
                            (v.Company != null && v.Company.Name != null && EF.Functions.Like(v.Company.Name.ToLower(), like))
                        );
                        break;
                }
            }

            return await q.AsNoTracking().CountAsync();
        }

        public async Task<VisaRequest?> GetByIdAsync(int id)
            => await _context.VisaRequests
                             .Include(v => v.Attachments)
                             .AsNoTracking()
                             .FirstOrDefaultAsync(v => v.Id == id);

        public async Task UpdateAsync(VisaRequest entity)
        {
            var existing = await _context.VisaRequests
                                         .FirstOrDefaultAsync(v => v.Id == entity.Id);

            if (existing == null)
                throw new KeyNotFoundException($"VisaRequest {entity.Id} not found.");

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
            existing.Status = entity.Status;
            existing.Reason = entity.Reason;

            await _context.SaveChangesAsync();
        }
    }
}
