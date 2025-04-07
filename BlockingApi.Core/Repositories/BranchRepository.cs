using BlockingApi.Data.Context;
using BlockingApi.Data.Models;
using Microsoft.EntityFrameworkCore;

public class BranchRepository : IBranchRepository
{
    private readonly BlockingApiDbContext _context;

    public BranchRepository(BlockingApiDbContext context)
    {
        _context = context;
    }

    // Method to fetch a branch by its code
    public async Task<Branch> GetBranchById(string branchCode)
    {
        return await _context.Branches
            .Include(b => b.Area) // Include the Area navigation property.
            .FirstOrDefaultAsync(b => b.CABBN == branchCode)
               ?? throw new InvalidOperationException("Branch not found.");
    }

}
