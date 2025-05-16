using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class BankAccountRepository : IBankAccountRepository
    {
        private readonly CompGateApiDbContext _context;
        public BankAccountRepository(CompGateApiDbContext context) => _context = context;

        public async Task CreateAsync(BankAccount account)
        {
            await _context.BankAccounts.AddAsync(account);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var acct = await _context.BankAccounts.FindAsync(id);
            if (acct != null)
            {
                _context.BankAccounts.Remove(acct);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IList<BankAccount>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit)
        {
            IQueryable<BankAccount> q = _context.BankAccounts.Include(b => b.Currency).AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "accountnumber":
                        q = q.Where(b => b.AccountNumber.Contains(searchTerm));
                        break;
                    case "userid":
                        if (int.TryParse(searchTerm, out var uid))
                            q = q.Where(b => b.UserId == uid);
                        break;
                    default:
                        q = q.Where(b =>
                           b.AccountNumber.Contains(searchTerm) ||
                           b.UserId.ToString().Contains(searchTerm));
                        break;
                }
            }

            return await q
                .OrderBy(b => b.Id)
                .Skip((page - 1) * limit)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<BankAccount?> GetByIdAsync(int id)
        {
            return await _context.BankAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<int> GetCountAsync(string? searchTerm, string? searchBy)
        {
            IQueryable<BankAccount> q = _context.BankAccounts;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "accountnumber":
                        q = q.Where(b => b.AccountNumber.Contains(searchTerm));
                        break;
                    case "userid":
                        if (int.TryParse(searchTerm, out var uid))
                            q = q.Where(b => b.UserId == uid);
                        break;
                    default:
                        q = q.Where(b =>
                           b.AccountNumber.Contains(searchTerm) ||
                           b.UserId.ToString().Contains(searchTerm));
                        break;
                }
            }

            return await q.CountAsync();
        }

        public async Task UpdateAsync(BankAccount account)
        {
            _context.BankAccounts.Update(account);
            await _context.SaveChangesAsync();
        }
    }
}
