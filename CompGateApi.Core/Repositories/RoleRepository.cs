using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using CompGateApi.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CompGateApi.Core.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly CompGateApiDbContext _context;

        public RoleRepository(CompGateApiDbContext context)
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
