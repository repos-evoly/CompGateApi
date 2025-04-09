using CardOpsApi.Data.Context;
using CardOpsApi.Data.Models;
using CardOpsApi.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CardOpsApi.Core.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly CardOpsApiDbContext _context;

        public RoleRepository(CardOpsApiDbContext context)
        {
            _context = context;
        }

        public async Task<List<string>> GetUserPermissions(int userId)
        {
            return await _context.UserRolePermissions
                .Where(urp => urp.UserId == userId)
                .Select(urp => urp.Permission.Name)
                .ToListAsync();
        }

        public async Task<User> GetUserByAuthUserId(int authUserId)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.AuthUserId == authUserId)
                ?? throw new InvalidOperationException("User not found.");
        }


    }
}
