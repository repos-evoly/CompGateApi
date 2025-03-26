using BlockingApi.Core.Abstractions;
using BlockingApi.Core.Dtos;
using BlockingApi.Data.Context;
using BlockingApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlockingApi.Core.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly BlockingApiDbContext _context;
        private readonly HttpClient _httpClient;

        public UserRepository(BlockingApiDbContext context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
        }

        public async Task<bool> AddUser(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var rolePermissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == user.RoleId)
                .ToListAsync();

            var userRolePermissions = rolePermissions.Select(rp => new UserRolePermission
            {
                UserId = user.Id,
                RoleId = rp.RoleId,
                PermissionId = rp.PermissionId
            }).ToList();

            _context.UserRolePermissions.AddRange(userRolePermissions);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> AssignRole(int userId, int roleId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            user.RoleId = roleId;
            await _context.SaveChangesAsync();

            await RemoveRolePermissions(userId);
            await AssignDefaultPermissions(userId, roleId);

            return true;
        }

        public async Task<bool> AssignUserPermissions(int userId, List<UserPermissionAssignmentDto> permissions)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            var existingPermissions = _context.UserRolePermissions.Where(up => up.UserId == userId);
            _context.UserRolePermissions.RemoveRange(existingPermissions);

            var userPermissions = permissions.Select(p => new UserRolePermission
            {
                UserId = userId,
                PermissionId = p.PermissionId,
                RoleId = p.RoleId
            }).ToList();

            _context.UserRolePermissions.AddRange(userPermissions);
            await _context.SaveChangesAsync();

            return true;
        }


        public async Task<List<PermissionStatusDto>> GetUserPermissions(int userId)
        {
            // Get all permissions from the database
            var allPermissions = await _context.Permissions.ToListAsync();

            // Get the user's current permissions
            var userPermissions = await _context.UserRolePermissions
                .Where(urp => urp.UserId == userId)
                .Select(urp => urp.PermissionId)
                .ToListAsync();

            // Prepare the result with permission status
            var permissionStatusList = allPermissions.Select(p => new PermissionStatusDto
            {
                PermissionId = p.Id,
                PermissionName = p.Name,
                // If the user has the permission, return 1, else 0
                HasPermission = userPermissions.Contains(p.Id) ? 1 : 0
            }).ToList();

            return permissionStatusList;
        }



        public async Task<bool> RemoveRolePermissions(int userId)
        {
            var existingPermissions = _context.UserRolePermissions.Where(up => up.UserId == userId);
            _context.UserRolePermissions.RemoveRange(existingPermissions);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<UserDetailsDto>> GetUsers(string authToken)
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Branch)
                .ToListAsync();

            var userDetailsList = new List<UserDetailsDto>();

            foreach (var user in users)
            {
                var authUser = await FetchAuthUserDetails(user.Id, authToken);

                var userDto = new UserDetailsDto
                {
                    AuthUserId = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Phone = user.Phone,
                    Role = user.Role?.NameLT ?? "Unknown",
                    Branch = user.Branch?.Name ?? "Unknown",
                    IsTwoFactorEnabled = authUser?.IsTwoFactorEnabled ?? false,
                    PasswordResetToken = authUser?.PasswordResetToken
                };

                userDetailsList.Add(userDto);
            }

            return userDetailsList;
        }

        public async Task<UserDetailsDto?> GetUserById(int userId, string authToken)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Branch)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return null;

            var authUser = await FetchAuthUserDetails(userId, authToken);

            return new UserDetailsDto
            {
                AuthUserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role?.NameLT ?? "Unknown",
                Branch = user.Branch?.Name ?? "Unknown",
                BranchId = user.Branch?.Id ?? 0,
                RoleId = user.Role?.Id ?? 0,
                IsTwoFactorEnabled = authUser?.IsTwoFactorEnabled ?? false,
                PasswordResetToken = authUser?.PasswordResetToken
            };
        }

        private async Task<AuthUserDto?> FetchAuthUserDetails(int userId, string authToken)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                var response = await _httpClient.GetAsync($"http://10.3.3.11/authapi/api/users/{userId}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<AuthUserDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch
            {
                // Log error if needed
            }
            return null;
        }

        private async Task AssignDefaultPermissions(int userId, int roleId)
        {
            var rolePermissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            var userPermissions = rolePermissions.Select(permissionId => new UserRolePermission
            {
                UserId = userId,
                RoleId = roleId,
                PermissionId = permissionId
            }).ToList();

            _context.UserRolePermissions.AddRange(userPermissions);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> EditUser(int userId, EditUserDto editUserDto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            user.FirstName = editUserDto.FirstName;
            user.LastName = editUserDto.LastName;
            user.Email = editUserDto.Email;
            user.Phone = editUserDto.Phone;
            user.RoleId = editUserDto.RoleId;
            user.BranchId = editUserDto.BranchId;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Role>> GetRoles()
        {
            return await _context.Roles.ToListAsync();
        }

        public async Task<List<Permission>> GetPermissions()
        {
            return await _context.Permissions.ToListAsync();
        }
    }
}
