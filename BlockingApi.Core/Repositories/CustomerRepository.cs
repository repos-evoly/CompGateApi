using BlockingApi.Data.Context;
using BlockingApi.Data.Models;
using BlockingApi.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlockingApi.Core.Repositories
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly BlockingApiDbContext _context;

        public CustomerRepository(BlockingApiDbContext context)
        {
            _context = context;
        }

        public async Task<bool> BlockCustomer(string customerId, int reasonId, int sourceId, int blockedByUserId)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CID == customerId);
            if (customer == null) return false;

            var blockRecord = new BlockRecord
            {
                CustomerId = customer.Id,
                ReasonId = reasonId,
                SourceId = sourceId,
                BlockDate = DateTimeOffset.Now,
                BlockedByUserId = blockedByUserId,
                Status = "Blocked"
            };

            _context.BlockRecords.Add(blockRecord);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UnblockCustomer(string customerId, int unblockedByUserId)
        {
            var block = await _context.BlockRecords
                .Include(b => b.Customer)
                .Where(b => b.Customer.CID == customerId && b.ActualUnblockDate == null)
                .OrderByDescending(b => b.BlockDate)
                .FirstOrDefaultAsync();

            if (block == null) return false;

            block.ActualUnblockDate = DateTimeOffset.Now;
            block.Status = "Unblocked";
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<Customer>> GetBlockedCustomers()
        {
            return await _context.Customers
                .Include(c => c.Branch)
                .Include(c => c.BlockRecords).ThenInclude(b => b.BlockedBy)
                .Include(c => c.BlockRecords).ThenInclude(b => b.Reason)
                .Include(c => c.BlockRecords).ThenInclude(b => b.Source)
                .Where(c => c.BlockRecords.Any(b => b.ActualUnblockDate == null))
                .ToListAsync();
        }


        public async Task<List<Customer>> SearchCustomers(string searchTerm)
        {
            return await _context.Customers
                .Where(c => c.CID.Contains(searchTerm) ||
                            c.FirstName.Contains(searchTerm) ||
                            c.LastName.Contains(searchTerm) ||
                            (c.Email != null && c.Email.Contains(searchTerm)))
                .Include(c => c.Branch)
                .Include(c => c.BlockRecords)
                    .ThenInclude(b => b.BlockedBy)
                .Include(c => c.BlockRecords)
                    .ThenInclude(b => b.UnblockedBy)
                .Include(c => c.BlockRecords)
                    .ThenInclude(b => b.Reason)
                .Include(c => c.BlockRecords)
                    .ThenInclude(b => b.Source)
                .ToListAsync();
        }

        public async Task<List<Customer>> GetUnblockedCustomers()
        {
            return await _context.Customers
                .Include(c => c.Branch)
                .Include(c => c.BlockRecords).ThenInclude(b => b.BlockedBy)
                .Include(c => c.BlockRecords).ThenInclude(b => b.UnblockedBy)
                .Include(c => c.BlockRecords).ThenInclude(b => b.Reason)
                .Include(c => c.BlockRecords).ThenInclude(b => b.Source)
                .Where(c => c.BlockRecords.Any(b => b.ActualUnblockDate != null))
                .ToListAsync();
        }

        // New: Get blocked customers with optional search and pagination.
        public async Task<List<Customer>> GetBlockedCustomers(string? search, string? searchBy, int page, int limit)
        {
            var query = _context.Customers
                .Include(c => c.Branch)
                .Include(c => c.BlockRecords).ThenInclude(b => b.BlockedBy)
                .Include(c => c.BlockRecords).ThenInclude(b => b.Reason)
                .Include(c => c.BlockRecords).ThenInclude(b => b.Source)
                .Where(c => c.BlockRecords.Any(b => b.ActualUnblockDate == null))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search) && !string.IsNullOrWhiteSpace(searchBy))
            {
                search = search.Trim();
                switch (searchBy.ToLower())
                {
                    case "firstname":
                        query = query.Where(c => c.FirstName.Contains(search));
                        break;
                    case "lastname":
                        query = query.Where(c => c.LastName.Contains(search));
                        break;
                    case "email":
                        query = query.Where(c => c.Email.Contains(search));
                        break;
                    case "cid":
                        query = query.Where(c => c.CID.Contains(search));
                        break;
                    default:
                        break;
                }
            }

            query = query.OrderBy(c => c.Id)
                         .Skip((page - 1) * limit)
                         .Take(limit);

            return await query.ToListAsync();
        }

        // New: Get unblocked customers with optional search and pagination.
        public async Task<List<Customer>> GetUnblockedCustomers(string? search, string? searchBy, int page, int limit)
        {
            var query = _context.Customers
                .Include(c => c.Branch)
                .Include(c => c.BlockRecords).ThenInclude(b => b.BlockedBy)
                .Include(c => c.BlockRecords).ThenInclude(b => b.UnblockedBy)
                .Include(c => c.BlockRecords).ThenInclude(b => b.Reason)
                .Include(c => c.BlockRecords).ThenInclude(b => b.Source)
                .Where(c => c.BlockRecords.Any(b => b.ActualUnblockDate != null))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search) && !string.IsNullOrWhiteSpace(searchBy))
            {
                search = search.Trim();
                switch (searchBy.ToLower())
                {
                    case "firstname":
                        query = query.Where(c => c.FirstName.Contains(search));
                        break;
                    case "lastname":
                        query = query.Where(c => c.LastName.Contains(search));
                        break;
                    case "email":
                        query = query.Where(c => c.Email.Contains(search));
                        break;
                    case "cid":
                        query = query.Where(c => c.CID.Contains(search));
                        break;
                    default:
                        break;
                }
            }

            query = query.OrderBy(c => c.Id)
                         .Skip((page - 1) * limit)
                         .Take(limit);

            return await query.ToListAsync();
        }

        public async Task<int> GetBlockedAccountsCountAsync()
        {
            return await _context.Customers
                .Where(c => c.BlockRecords.Any(b => b.Status == "Blocked"))
                .CountAsync();
        }

        public async Task<int> GetBlockedUsersTodayCountAsync()
        {
            var today = DateTimeOffset.Now.Date;
            return await _context.Customers
                .Where(c => c.BlockRecords
                    .Any(b => b.Status == "Blocked" && b.BlockDate.Date == today))
                .CountAsync();
        }
    }
}

