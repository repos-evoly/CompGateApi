using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

        // Public: KYC lookup via external service
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

        // Check if external KYC returned a non-empty companyId
        public async Task<bool> CanRegisterCompanyAsync(string companyCode)
        {
            var kyc = await LookupKycAsync(companyCode);
            return kyc != null && !string.IsNullOrWhiteSpace(kyc.companyId);
        }


        // Register both Company and its admin user

        public async Task<CompanyRegistrationResult> RegisterCompanyAdminAsync(CompanyRegistrationDto dto)

        {
            var result = new CompanyRegistrationResult();
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // 1) Duplicate guard
                if (await _db.Companies.AnyAsync(c => c.Code == dto.CompanyCode))
                    return new CompanyRegistrationResult
                    {
                        Error = "A company with that code already exists."
                    };

                // 2) KYC guard
                var kyc = await LookupKycAsync(dto.CompanyCode);
                if (kyc == null)
                    return new CompanyRegistrationResult
                    {
                        Error = "KYC service unavailable or returned an error."
                    };
                if (string.IsNullOrWhiteSpace(kyc.companyId))
                    return new CompanyRegistrationResult
                    {
                        Error = "KYC lookup did not return a valid company ID."
                    };

                // 3) Create the Company
                var company = new Company
                {
                    Code = dto.CompanyCode,
                    Name = kyc.legalCompanyName,
                    //needs fix isActive
                    IsActive = true,
                    KycRequestedAt = DateTimeOffset.UtcNow,
                    RegistrationStatus = RegistrationStatus.UnderReview,
                    RegistrationStatusMessage = null,
                    ServicePackageId = 1,
                    CommissionOnReceiver = false,
                };
                _db.Companies.Add(company);
                await _db.SaveChangesAsync();

                // 4) Register in Auth service
                _logger.LogInformation("Calling AuthApi/api/auth/register for {Company}", dto.CompanyCode);
                using var authClient = _httpFactory.CreateClient("AuthApi");
                var authPayload = new
                {
                    username = dto.Username,
                    fullNameLT = dto.FirstName,
                    fullNameAR = dto.LastName,
                    email = dto.Email,
                    password = dto.Password,
                    roleId = dto.RoleId,
                    KycBranchId = kyc.branchId,
                    KycLegalCompanyName = kyc.legalCompanyName,
                    KycLegalCompanyNameLt = kyc.legalCompanyNameLT
                };
                using var authResp = await authClient.PostAsJsonAsync("api/auth/register", authPayload);

                var body = await authResp.Content.ReadAsStringAsync();
                _logger.LogInformation("AuthApi replied {Status}: {Body}", authResp.StatusCode, body);

                if (!authResp.IsSuccessStatusCode)
                    return new CompanyRegistrationResult
                    {
                        Error = $"Username or Email Already Exists {(int)authResp.StatusCode}"
                    };

                var reg = JsonSerializer.Deserialize<AuthRegisterResponseDto>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (reg == null || reg.userId == 0)
                    return new CompanyRegistrationResult
                    {
                        Error = "Auth service returned an invalid payload."
                    };

                // 5) Persist the local admin user
                var user = new User
                {
                    AuthUserId = reg.userId,
                    CompanyId = company.Id,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    Email = dto.Email,
                    Phone = dto.Phone,
                    RoleId = dto.RoleId,
                    IsCompanyAdmin = true,

                };
                var added = await _userRepo.AddUser(user);
                if (!added)
                    return new CompanyRegistrationResult
                    {
                        Error = "Failed to save local admin‐user record."
                    };

                await tx.CommitAsync();
                return new CompanyRegistrationResult
                {
                    Success = true,
                    Location = $"/api/companies/{dto.CompanyCode}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during company‐admin registration, rolling back");
                await tx.RollbackAsync();
                return new CompanyRegistrationResult
                {
                    Error = "Unexpected server error. Check logs for details."
                };
            }
        }
        public async Task<bool> UpdateCompanyStatusAsync(
            string companyCode,
            CompanyStatusUpdateDto dto)
        {
            var c = await _db.Companies
                             .FirstOrDefaultAsync(x => x.Code == companyCode);
            if (c == null) return false;

            c.RegistrationStatus = dto.Status;
            c.RegistrationStatusMessage = dto.Message;
            c.KycReviewedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }


        // Fetch all companies with their admin's KYC details
        public async Task<IList<CompanyListDto>> GetAllCompaniesAsync(
      string? searchTerm,
      RegistrationStatus? statusFilter,
      int page,
      int limit)
        {
            var q = _db.Companies
                       .AsNoTracking()
                       .Include(c => c.ServicePackage)
                       .AsQueryable(); // ✅ fix the type mismatch

            if (!string.IsNullOrWhiteSpace(searchTerm))
                q = q.Where(c =>
                    c.Code.Contains(searchTerm) ||
                    (c.Name != null && c.Name.Contains(searchTerm)));

            if (statusFilter.HasValue)
                q = q.Where(c => c.RegistrationStatus == statusFilter.Value);

            return await q
                .OrderBy(c => c.Code)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(c => new CompanyListDto
                {
                    Code = c.Code,
                    Name = c.Name,
                    IsActive = c.IsActive,
                    RegistrationStatus = c.RegistrationStatus,
                    RegistrationStatusMessage = c.RegistrationStatusMessage,
                    KycRequestedAt = c.KycRequestedAt,
                    KycReviewedAt = c.KycReviewedAt,
                    KycBranchId = c.KycBranchId,
                    KycLegalCompanyName = c.KycLegalCompanyName,
                    KycLegalCompanyNameLt = c.KycLegalCompanyNameLt,
                    KycMobile = c.KycMobile,
                    KycNationality = c.KycNationality,
                    KycCity = c.KycCity,
                    ServicePackageId = c.ServicePackageId,
                    ServicePackageName = c.ServicePackage.Name
                })
                .ToListAsync();
        }


        // Count companies matching filters
        public async Task<int> GetCompaniesCountAsync(string? searchTerm, RegistrationStatus? statusFilter)
        {
            var q = _db.Companies.AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
                q = q.Where(c =>
                    c.Code.Contains(searchTerm) ||
                    (c.KycLegalCompanyNameLt != null && c.KycLegalCompanyNameLt.Contains(searchTerm)));
            if (statusFilter.HasValue)
                q = q.Where(c => c.RegistrationStatus == statusFilter.Value);
            return await q.CountAsync();
        }

        // Helper: fetch Company entity by its code
        public async Task<Company?> GetByCodeAsync(string code)
        {
            return await _db.Companies.Include(c => c.Attachments)
                                        .Include(c => c.ServicePackage)
                                        .FirstOrDefaultAsync(c => c.Code == code);
        }

        public async Task CreateAsync(Company company)
        {
            _db.Companies.Add(company);
            await _db.SaveChangesAsync();
        }

    }
}
