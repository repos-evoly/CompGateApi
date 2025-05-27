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
        public RtgsRequestRepository(CompGateApiDbContext context)
            => _context = context;

        // COMPANY: only user’s own
        public async Task<IList<RtgsRequest>> GetAllByUserAsync(
            int userId, string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.RtgsRequests.Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "paymenttype":
                        q = q.Where(r => r.PaymentType!.Contains(searchTerm));
                        break;
                    case "beneficiarybank":
                        q = q.Where(r => r.BeneficiaryBank!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.ApplicantName!.Contains(searchTerm) ||
                            r.BeneficiaryName!.Contains(searchTerm) ||
                            r.BranchName!.Contains(searchTerm));
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
            var q = _context.RtgsRequests.Where(r => r.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "paymenttype":
                        q = q.Where(r => r.PaymentType!.Contains(searchTerm));
                        break;
                    case "beneficiarybank":
                        q = q.Where(r => r.BeneficiaryBank!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.ApplicantName!.Contains(searchTerm) ||
                            r.BeneficiaryName!.Contains(searchTerm) ||
                            r.BranchName!.Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }

        // ADMIN: all requests
        public async Task<IList<RtgsRequest>> GetAllAsync(
            string? searchTerm, string? searchBy, int page, int limit)
        {
            var q = _context.RtgsRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "paymenttype":
                        q = q.Where(r => r.PaymentType!.Contains(searchTerm));
                        break;
                    case "beneficiarybank":
                        q = q.Where(r => r.BeneficiaryBank!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.ApplicantName!.Contains(searchTerm) ||
                            r.BeneficiaryName!.Contains(searchTerm) ||
                            r.BranchName!.Contains(searchTerm));
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
            var q = _context.RtgsRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "paymenttype":
                        q = q.Where(r => r.PaymentType!.Contains(searchTerm));
                        break;
                    case "beneficiarybank":
                        q = q.Where(r => r.BeneficiaryBank!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.ApplicantName!.Contains(searchTerm) ||
                            r.BeneficiaryName!.Contains(searchTerm) ||
                            r.BranchName!.Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }

        public async Task<RtgsRequest?> GetByIdAsync(int id)
            => await _context.RtgsRequests
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

        public async Task<IList<RtgsRequest>> GetAllByCompanyAsync(
    int companyId,
    string? searchTerm,
    string? searchBy,
    int page,
    int limit)
        {
            var q = _context.RtgsRequests
                            .Where(r => r.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "paymenttype":
                        q = q.Where(r => r.PaymentType!.Contains(searchTerm));
                        break;
                    case "beneficiarybank":
                        q = q.Where(r => r.BeneficiaryBank!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.ApplicantName!.Contains(searchTerm) ||
                            r.BeneficiaryName!.Contains(searchTerm) ||
                            r.BranchName!.Contains(searchTerm));
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
            int companyId,
            string? searchTerm,
            string? searchBy)
        {
            var q = _context.RtgsRequests
                            .Where(r => r.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch ((searchBy ?? "").ToLower())
                {
                    case "paymenttype":
                        q = q.Where(r => r.PaymentType!.Contains(searchTerm));
                        break;
                    case "beneficiarybank":
                        q = q.Where(r => r.BeneficiaryBank!.Contains(searchTerm));
                        break;
                    default:
                        q = q.Where(r =>
                            r.ApplicantName!.Contains(searchTerm) ||
                            r.BeneficiaryName!.Contains(searchTerm) ||
                            r.BranchName!.Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }

    }
}
