using BlockingApi.Data.Context;
using BlockingApi.Data.Models;
using BlockingApi.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlockingApi.Core.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly BlockingApiDbContext _context;

        public RoleRepository(BlockingApiDbContext context)
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

    }
}
