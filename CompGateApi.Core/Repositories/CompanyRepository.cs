// CompGateApi.Core.Repositories/CompanyRepository.cs
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CompGateApi.Core.Repositories
{
    public class CompanyRepository : ICompanyRepository
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IUserRepository _userRepo;
        private readonly CompGateApiDbContext _db;
        private readonly ILogger<CompanyRepository> _logger;

        public CompanyRepository(
            IHttpClientFactory httpFactory,
            IUserRepository userRepo,
            CompGateApiDbContext db,
            ILogger<CompanyRepository> logger)
        {
            _httpFactory = httpFactory;
            _userRepo = userRepo;
            _db = db;
            _logger = logger;
        }

        public async Task<KycDto?> LookupKycAsync(string companyCode)
        {
            try
            {
                var client = _httpFactory.CreateClient("KycApi");
                using var resp = await client.GetAsync($"/kycapi/bcd/{companyCode}");
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadFromJsonAsync<KycDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KYC lookup failed for {Code}", companyCode);
                return null;
            }
        }

        // in CompanyRepository.cs

        public async Task<bool> CanRegisterCompanyAsync(string companyCode)
        {
            var kyc = await LookupKycAsync(companyCode);
            // only allow if we got back a non‐empty companyId
            return kyc != null && !string.IsNullOrWhiteSpace(kyc.companyId);
        }


        public async Task<bool> RegisterCompanyAdminAsync(CompanyRegistrationDto dto)
        {
            // 1) Auth‐service registration
            var authClient = _httpFactory.CreateClient("AuthApi");
            var authPayload = new
            {
                username = dto.Username,
                fullNameLT = dto.FirstName,
                fullNameAR = dto.LastName,
                email = dto.Email,
                password = dto.Password,
                roleId = dto.RoleId
            };
            using var authResp = await authClient.PostAsJsonAsync(
                "/compauthapi/api/auth/register", authPayload);
            if (!authResp.IsSuccessStatusCode) return false;

            var reg = await authResp.Content
                                     .ReadFromJsonAsync<AuthRegisterResponseDto>();
            if (reg == null || reg.userId == 0) return false;

            // 2) KYC fetch so we can stamp user
            var kyc = await LookupKycAsync(dto.CompanyId);

            // 3) Build local User
            var user = new User
            {
                AuthUserId = reg.userId,
                CompanyId = dto.CompanyId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Phone = dto.Phone,
                RoleId = dto.RoleId,
                ServicePackageId = 1,
                IsCompanyAdmin = true,
                KycStatus = KycStatus.UnderReview,
                KycRequestedAt = DateTime.UtcNow,
                KycBranchId = kyc?.branchId,
                KycLegalCompanyName = kyc?.legalCompanyName,
                KycLegalCompanyNameLt = kyc?.legalCompanyNameLT
            };

            // 4) Persist (wires default perms)
            return await _userRepo.AddUser(user);
        }

        public async Task<bool> UpdateCompanyStatusAsync(
            string companyCode,
            CompanyStatusUpdateDto dto)
        {
            var admin = await _db.Users
                .FirstOrDefaultAsync(u => u.CompanyId == companyCode && u.IsCompanyAdmin);
            if (admin == null) return false;

            admin.KycStatus = dto.Status;
            admin.KycStatusMessage = dto.Message;
            admin.KycReviewedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }


        public async Task<IList<CompanyListDto>> GetAllCompaniesAsync(
               string? searchTerm,
               KycStatus? statusFilter,
               int page,
               int limit)
        {
            var q = _db.Users
                .AsNoTracking()
                .Where(u => u.IsCompanyAdmin);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                q = q.Where(u =>
                    u.CompanyId!.Contains(searchTerm) ||
                    (u.KycLegalCompanyName != null && u.KycLegalCompanyName.Contains(searchTerm)));
            }
            if (statusFilter.HasValue)
            {
                q = q.Where(u => u.KycStatus == statusFilter.Value);
            }

            var list = await q
                .OrderBy(u => u.CompanyId)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(u => new CompanyListDto
                {
                    CompanyId = u.CompanyId!,
                    BranchId = u.KycBranchId,
                    LegalNameEn = u.KycLegalCompanyName,
                    LegalNameLt = u.KycLegalCompanyNameLt,
                    Status = u.KycStatus,
                    StatusMessage = u.KycStatusMessage,
                    RequestedAt = u.KycRequestedAt,
                    ReviewedAt = u.KycReviewedAt
                })
                .ToListAsync();

            return list;
        }

        public async Task<int> GetCompaniesCountAsync(
            string? searchTerm,
            KycStatus? statusFilter)
        {
            var q = _db.Users.Where(u => u.IsCompanyAdmin);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                q = q.Where(u =>
                    u.CompanyId!.Contains(searchTerm) ||
                    (u.KycLegalCompanyName != null && u.KycLegalCompanyName.Contains(searchTerm)));
            }
            if (statusFilter.HasValue)
            {
                q = q.Where(u => u.KycStatus == statusFilter.Value);
            }

            return await q.CountAsync();
        }
    }
}