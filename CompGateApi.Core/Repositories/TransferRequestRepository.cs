// CompGateApi.Data.Repositories/TransferRequestRepository.cs
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CompGateApi.Data.Repositories
{
    public class TransferRequestRepository : ITransferRequestRepository
    {
        private readonly CompGateApiDbContext _db;
        private readonly IHttpClientFactory _httpFactory;

        public TransferRequestRepository(CompGateApiDbContext db, IHttpClientFactory httpFactory)
        {
            _db = db;
            _httpFactory = httpFactory;
        }

        // ── USER ──────────────────────────────────────────────────────────────────

        public async Task<List<TransferRequest>> GetAllByUserAsync(
            int userId, string? searchTerm, int page, int limit)
        {
            var q = _db.TransferRequests
                .Include(t => t.TransactionCategory)
                .Include(t => t.Currency)
                .Include(t => t.ServicePackage)
                .Where(t => t.UserId == userId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
                q = q.Where(t =>
                    t.FromAccount.Contains(searchTerm!) ||
                    t.ToAccount.Contains(searchTerm!) ||
                    t.Status.Contains(searchTerm!));

            return await q
                .OrderByDescending(t => t.RequestedAt)
                .Skip((page - 1) * limit).Take(limit)
                .AsNoTracking().ToListAsync();
        }

        public async Task<int> GetCountByUserAsync(int userId, string? searchTerm)
        {
            var q = _db.TransferRequests.Where(t => t.UserId == userId);
            if (!string.IsNullOrWhiteSpace(searchTerm))
                q = q.Where(t =>
                    t.FromAccount.Contains(searchTerm!) ||
                    t.ToAccount.Contains(searchTerm!) ||
                    t.Status.Contains(searchTerm!));
            return await q.CountAsync();
        }

        public async Task<TransferRequest?> GetByIdAsync(int id) =>
            await _db.TransferRequests
                .Include(t => t.TransactionCategory)
                .Include(t => t.Currency)
                .Include(t => t.ServicePackage)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

        public async Task CreateAsync(TransferRequest e)
        {
            _db.TransferRequests.Add(e);
            await _db.SaveChangesAsync();
        }

        // ── EXTERNAL CALLS ────────────────────────────────────────────────────────

        public async Task<List<AccountDto>> GetAccountsAsync(string codeOrAccount)
        {
            // 1) Derive the 6-digit customer code
            string code = codeOrAccount.Length == 6
                ? codeOrAccount
                : codeOrAccount.Length == 13
                    ? codeOrAccount.Substring(4, 6)
                    : throw new ArgumentException("Must be 6 or 13 digits", nameof(codeOrAccount));

            // 2) Build & call the bank API
            var client = _httpFactory.CreateClient("BankApi");
            var payload = new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = Guid.NewGuid().ToString("N").Substring(0, 16),
                    userName = "TEDMOB",
                    customerNumber = code,
                    requestTime = DateTime.UtcNow.ToString("o"),
                    language = "AR"
                },
                Details = new Dictionary<string, string>
        {
            { "@CID",   code },
            { "@GETAVB","Y" }
        }
            };

            var resp = await client.PostAsJsonAsync("/api/mobile/accounts", payload);
            if (!resp.IsSuccessStatusCode)
                return new List<AccountDto>();

            var bankDto = await resp.Content.ReadFromJsonAsync<ExternalAccountsResponseDto>();
            if (bankDto?.Details?.Accounts == null)
                return new List<AccountDto>();

            // 3) Reconstruct each full 13-digit account string
            return bankDto.Details.Accounts
                .Select(a => new AccountDto
                {
                    AccountString = $"{a.YBCD01AB}{a.YBCD01AN}{a.YBCD01AS}".Trim()
                })
                .ToList();
        }
        public async Task<List<StatementEntryDto>> GetStatementAsync(string account, DateTime from, DateTime to)
        {
            var client = _httpFactory.CreateClient("BankApi");
            var payload = new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = Guid.NewGuid().ToString("N").Substring(0, 16),
                    userName = "TEDMOB",
                    customerNumber = account,
                    requestTime = DateTime.UtcNow.ToString("o"),
                    language = "AR"
                },
                Details = new Dictionary<string, string>
                {
                    { "@ACC",   account },
                    { "@BYDTE", "Y" },
                    { "@FDATE", from.ToString("yyyyMMdd") },
                    { "@TDATE", to.ToString("yyyyMMdd") },
                    { "@BYNBR", "N" },
                    { "@NBR",   "0" }
                }
            };

            var resp = await client.PostAsJsonAsync("/api/mobile/transactions", payload);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 1) If no "Details" object, return empty
            if (!root.TryGetProperty("Details", out var detailsElem)
                || detailsElem.ValueKind != JsonValueKind.Object)
            {
                return new List<StatementEntryDto>();
            }

            // 2) If "Transactions" is missing or not an array, return empty
            if (!detailsElem.TryGetProperty("Transactions", out var txnsElem)
                || txnsElem.ValueKind != JsonValueKind.Array)
            {
                return new List<StatementEntryDto>();
            }

            // 3) Parse each entry
            var list = new List<StatementEntryDto>();
            foreach (var el in txnsElem.EnumerateArray())
            {
                // guard against missing fields
                var date = el.GetProperty("YBCD04POD").GetString()?.Trim() ?? string.Empty;
                var drcr = el.GetProperty("YBCD04DRCR").GetString() ?? string.Empty;
                var amt = el.GetProperty("YBCD04AMA").GetDecimal();

                var narrs = new List<string>();
                if (el.TryGetProperty("YBCD04NAR1", out var n1) && n1.GetString() is string s1 && s1.Trim() != "")
                    narrs.Add(s1.Trim());
                if (el.TryGetProperty("YBCD04NAR2", out var n2) && n2.GetString() is string s2 && s2.Trim() != "")
                    narrs.Add(s2.Trim());

                list.Add(new StatementEntryDto
                {
                    PostingDate = date,
                    DrCr = drcr,
                    Amount = amt,
                    Narratives = narrs
                });
            }

            return list;
        }

        // ── ADMIN ─────────────────────────────────────────────────────────────────

        public async Task<List<TransferRequest>> GetAllAsync(
            string? searchTerm, int page, int limit)
        {
            var q = _db.TransferRequests
                .Include(t => t.TransactionCategory)
                .Include(t => t.Currency)
                .Include(t => t.ServicePackage);

            if (!string.IsNullOrWhiteSpace(searchTerm))
                q = (Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<TransferRequest, ServicePackage>)q.Where(t =>
                    t.FromAccount.Contains(searchTerm!) ||
                    t.ToAccount.Contains(searchTerm!) ||
                    t.Status.Contains(searchTerm!));

            return await q
                .OrderByDescending(t => t.RequestedAt)
                .Skip((page - 1) * limit).Take(limit)
                .AsNoTracking().ToListAsync();
        }

        public async Task<int> GetCountAsync(string? searchTerm) =>
            await _db.TransferRequests
                     .Where(t =>
                        string.IsNullOrWhiteSpace(searchTerm) ||
                        t.FromAccount.Contains(searchTerm!) ||
                        t.ToAccount.Contains(searchTerm!) ||
                        t.Status.Contains(searchTerm!))
                     .CountAsync();

        public async Task UpdateAsync(TransferRequest e)
        {
            _db.TransferRequests.Update(e);
            await _db.SaveChangesAsync();
        }

        // ── HELPERS: external DTOs ────────────────────────────────────────────────

        private class ExternalAccountsResponseDto
        {
            public object Header { get; set; } = null!;
            public ExternalAccountsResponseDetailsDto Details { get; set; } = new();
        }
        private class ExternalAccountsResponseDetailsDto
        {
            public List<ExternalAccountDto> Accounts { get; set; } = new();
        }
        private class ExternalAccountDto
        {
            public string? YBCD01AB { get; set; }
            public string? YBCD01AN { get; set; }
            public string? YBCD01AS { get; set; }
        }
    }
}
