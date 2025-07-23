using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using CompGateApi.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Dtos;

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
                .Select(urp => urp.Permission.NameAr)
                .ToListAsync();
        }

        public async Task<User> GetUserByAuthUserId(int authUserId)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.AuthUserId == authUserId)
                ?? throw new InvalidOperationException("User not found.");
        }

        public async Task<List<RoleDto>> GetRolesAsync(bool? isGlobal = null)
        {
            var q = _context.Roles.AsQueryable();
            if (isGlobal.HasValue) q = q.Where(r => r.IsGlobal == isGlobal.Value);
            return await q
                .Select(r => new RoleDto
                {
                    Id = r.Id,
                    NameLT = r.NameLT,
                    NameAR = r.NameAR,
                    Description = r.Description,
                    IsGlobal = r.IsGlobal
                })
                .ToListAsync();
        }

        public async Task<RoleDto?> GetRoleByIdAsync(int roleId)
        {
            var r = await _context.Roles.FindAsync(roleId);
            if (r == null) return null;
            return new RoleDto
            {
                Id = r.Id,
                NameLT = r.NameLT,
                NameAR = r.NameAR,
                Description = r.Description,
                IsGlobal = r.IsGlobal
            };
        }

        public async Task<RoleDto> CreateRoleAsync(string nameLT, string nameAR, string description, bool isGlobal)
        {
            var role = new Role
            {
                NameLT = nameLT,
                NameAR = nameAR,
                Description = description,
                IsGlobal = isGlobal
            };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync();
            return new RoleDto
            {
                Id = role.Id,
                NameLT = role.NameLT,
                NameAR = role.NameAR,
                Description = role.Description,
                IsGlobal = role.IsGlobal
            };
        }

        public async Task<bool> UpdateRoleAsync(int roleId, string nameLT, string nameAR, string description, bool isGlobal)
        {
            var role = await _context.Roles.FindAsync(roleId);
            if (role == null) return false;
            role.NameLT = nameLT;
            role.NameAR = nameAR;
            role.Description = description;
            role.IsGlobal = isGlobal;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteRoleAsync(int roleId)
        {
            var role = await _context.Roles.FindAsync(roleId);
            if (role == null) return false;
            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<PermissionDto>> GetAllPermissionsAsync()
        {
            return await _context.Permissions
                .Select(p => new PermissionDto
                {
                    Id = p.Id,
                    NameAr = p.NameAr,
                    NameEn = p.NameEn,
                    Description = p.Description,
                    IsGlobal = p.IsGlobal
                })
                .ToListAsync();
        }

        public async Task<List<PermissionDto>> GetPermissionsByRoleAsync(int roleId)
        {
            return await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Include(rp => rp.Permission)
                .Select(rp => new PermissionDto
                {
                    Id = rp.Permission.Id,
                    NameAr = rp.Permission.NameAr,
                    NameEn = rp.Permission.NameEn,
                    Description = rp.Permission.Description,
                    IsGlobal = rp.Permission.IsGlobal
                })
                .Distinct()
                .ToListAsync();
        }

        public async Task<List<PermissionDto>> GetPermissionsByGlobalAsync(bool isGlobal)
        {
            var roleIds = await _context.Roles
                .Where(r => r.IsGlobal == isGlobal)
                .Select(r => r.Id)
                .ToListAsync();

            return await _context.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId) && rp.Permission.IsGlobal == isGlobal)
                .Include(rp => rp.Permission)
                .Select(rp => new PermissionDto
                {
                    Id = rp.Permission.Id,
                    NameAr = rp.Permission.NameAr,
                    NameEn = rp.Permission.NameEn,
                    Description = rp.Permission.Description,
                    IsGlobal = rp.Permission.IsGlobal
                })
                .Distinct()
                .ToListAsync();
        }

        public async Task<bool> AssignPermissionsToRoleAsync(int roleId, IEnumerable<int> permissionIds)
        {
            // remove existing
            var existing = _context.RolePermissions.Where(rp => rp.RoleId == roleId);
            _context.RolePermissions.RemoveRange(existing);

            // add new
            var toAdd = permissionIds.Select(pid => new RolePermission
            {
                RoleId = roleId,
                PermissionId = pid
            });
            _context.RolePermissions.AddRange(toAdd);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<PermissionDto> CreatePermissionAsync(string nameAr, string nameEn, string description, bool isGlobal)
        {
            var perm = new Permission
            {
                NameAr = nameAr,
                NameEn = nameEn,
                Description = description,
                IsGlobal = isGlobal
            };
            _context.Permissions.Add(perm);
            await _context.SaveChangesAsync();
            return new PermissionDto
            {
                Id = perm.Id,
                NameAr = perm.NameAr,
                NameEn = perm.NameEn,
                Description = perm.Description,
                IsGlobal = perm.IsGlobal
            };
        }

        public async Task<bool> UpdatePermissionAsync(int id, string nameAr, string nameEn, string description, bool isGlobal)
        {
            var perm = await _context.Permissions.FindAsync(id);
            if (perm == null) return false;
            perm.NameAr = nameAr;
            perm.NameEn = nameEn;
            perm.Description = description;
            perm.IsGlobal = isGlobal;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePermissionAsync(int id)
        {
            var perm = await _context.Permissions.FindAsync(id);
            if (perm == null) return false;
            _context.Permissions.Remove(perm);
            await _context.SaveChangesAsync();
            return true;
        }

    }
}
