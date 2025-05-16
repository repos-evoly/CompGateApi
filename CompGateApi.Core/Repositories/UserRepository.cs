using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CompGateApi.Core.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly CompGateApiDbContext _context;
        private readonly IHttpClientFactory _httpFactory;

        public UserRepository(CompGateApiDbContext context, IHttpClientFactory httpFactory)
        {
            _context = context;
            _httpFactory = httpFactory;
        }

        public async Task<bool> AddUser(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var perms = await _context.RolePermissions
                                     .Where(rp => rp.RoleId == user.RoleId)
                                     .ToListAsync();

            var ups = perms.Select(rp => new UserRolePermission
            {
                UserId = user.Id,
                RoleId = rp.RoleId,
                PermissionId = rp.PermissionId
            });

            _context.UserRolePermissions.AddRange(ups);
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
            // Retrieve all permissions from the database
            var allPermissions = await _context.Permissions.ToListAsync();

            // Retrieve the user's assigned permission Ids from the join table
            var userPermissionIds = await _context.UserRolePermissions
                .Where(urp => urp.UserId == userId)
                .Select(urp => urp.PermissionId)
                .ToListAsync();

            // Map to PermissionStatusDto: if the permission is assigned, mark HasPermission as 1; otherwise, 0.
            var permissionStatusList = allPermissions.Select(p => new PermissionStatusDto
            {
                PermissionId = p.Id,
                PermissionName = p.Name,
                HasPermission = userPermissionIds.Contains(p.Id) ? 1 : 0
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

        public async Task<List<UserDetailsDto>> GetUsersAsync(string? searchTerm, string? searchBy, int page, int limit, string authToken)
        {
            IQueryable<User> query = _context.Users.Include(u => u.Role);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "firstname":
                        query = query.Where(u => u.FirstName.Contains(searchTerm));
                        break;
                    case "lastname":
                        query = query.Where(u => u.LastName.Contains(searchTerm));
                        break;
                    case "email":
                        query = query.Where(u => u.Email.Contains(searchTerm));
                        break;
                    default:
                        query = query.Where(u => u.FirstName.Contains(searchTerm) || u.LastName.Contains(searchTerm) || u.Email.Contains(searchTerm));
                        break;
                }
            }

            var users = await query.OrderBy(u => u.Id)
                                   .Skip((page - 1) * limit)
                                   .Take(limit)
                                   .AsNoTracking()
                                   .ToListAsync();

            var userDetailsList = new List<UserDetailsDto>();
            foreach (var user in users)
            {
                // Reuse your existing logic to fetch additional auth info.
                var authUser = await FetchAuthUserDetails(user.Id, authToken);
                userDetailsList.Add(new UserDetailsDto
                {
                    UserId = user.Id,
                    AuthUserId = authUser?.Id ?? 0,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Phone = user.Phone,
                    Role = user.Role,
                    RoleId = user.Role?.Id ?? 0,
                    IsTwoFactorEnabled = authUser?.IsTwoFactorEnabled ?? false,
                    PasswordResetToken = authUser?.PasswordResetToken
                });
            }
            return userDetailsList;
        }

        public async Task<int> GetUserCountAsync(string? searchTerm, string? searchBy)
        {
            IQueryable<User> query = _context.Users;
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "firstname":
                        query = query.Where(u => u.FirstName.Contains(searchTerm));
                        break;
                    case "lastname":
                        query = query.Where(u => u.LastName.Contains(searchTerm));
                        break;
                    case "email":
                        query = query.Where(u => u.Email.Contains(searchTerm));
                        break;
                    default:
                        query = query.Where(u => u.FirstName.Contains(searchTerm) || u.LastName.Contains(searchTerm) || u.Email.Contains(searchTerm));
                        break;
                }
            }
            return await query.AsNoTracking().CountAsync();
        }


        public async Task<UserDetailsDto?> GetUserById(int userId, string authToken)
        {
            var user = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return null;

            var authUser = await FetchAuthUserDetails(userId, authToken);

            // Get the permission Ids assigned to the user via the join table
            var userPermissionIds = await _context.UserRolePermissions
                .Where(urp => urp.UserId == user.Id)
                .Select(urp => urp.PermissionId)
                .ToListAsync();

            // Retrieve the names of permissions the user has
            var userPermissionNames = await _context.Permissions
                .Where(p => userPermissionIds.Contains(p.Id))
                .Select(p => p.Name)
                .ToListAsync();

            return new UserDetailsDto
            {
                AuthUserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                RoleId = user.Role?.Id ?? 0,

                IsTwoFactorEnabled = authUser?.IsTwoFactorEnabled ?? false,
                PasswordResetToken = authUser?.PasswordResetToken,
                Permissions = userPermissionNames
            };
        }

        public async Task<UserDetailsDto?> GetUserByAuthId(int authId, string authToken)
        {
            // a) local lookup
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.AuthUserId == authId);
            if (user == null) return null;

            // b) auth details
            var authUser = await FetchAuthUserDetails(authId, authToken);

            // c) permissions
            var userPermissionIds = await _context.UserRolePermissions
                .Where(urp => urp.UserId == user.Id)
                .Select(urp => urp.PermissionId)
                .ToListAsync();
            var userPermissionNames = await _context.Permissions
                .Where(p => userPermissionIds.Contains(p.Id))
                .Select(p => p.Name)
                .ToListAsync();

            // d) bank accounts — only if CompanyId is set
            var accounts = new List<string>();
            if (!string.IsNullOrEmpty(user.CompanyId))
            {
                var bankClient = _httpFactory.CreateClient("BankApi");
                // optional: bankClient.DefaultRequestHeaders.Authorization = …
                var payload = new
                {
                    Header = new
                    {
                        system = "MOBILE",
                        referenceId = Guid.NewGuid().ToString("N").Substring(0, 16),
                        userName = "TEDMOB",
                        customerNumber = user.CompanyId,
                        requestTime = DateTime.UtcNow.ToString("o"),
                        language = "AR"
                    },
                    Details = new Dictionary<string, string> {
                    { "@CID",   user.CompanyId },
                    { "@GETAVB","Y" }
                }
                };

                var bankResp = await bankClient.PostAsJsonAsync("/api/mobile/accounts", payload);
                if (bankResp.IsSuccessStatusCode)
                {
                    var dto = await bankResp.Content.ReadFromJsonAsync<ExternalAccountsResponseDto>();
                    accounts = dto!.Details.Accounts
                        .Select(a => $"{a.YBCD01AB}{a.YBCD01AN}{a.YBCD01AS}".Trim())
                        .ToList();
                }
            }

            // e) build result
            return new UserDetailsDto
            {
                UserId = user.Id,
                AuthUserId = user.AuthUserId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                RoleId = user.Role.Id,
                IsTwoFactorEnabled = authUser?.IsTwoFactorEnabled ?? false,
                PasswordResetToken = authUser?.PasswordResetToken,
                Permissions = userPermissionNames,
                ServicePackageId = user.ServicePackageId,   // ← add this
                Accounts = accounts
            };
        }
        private async Task<AuthUserDto?> FetchAuthUserDetails(int authUserId, string authToken)
        {
            try
            {
                var client = _httpFactory.CreateClient("AuthApi");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", authToken);

                var resp = await client.GetAsync($"/api/users/{authUserId}");
                if (!resp.IsSuccessStatusCode) return null;

                var json = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<AuthUserDto>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null;
            }
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

        public async Task<List<BasicUserDto>> GetManagementUsersAsync(string currentUserRole)
        {
            // Define the management role names (all management-related roles).
            var managementRoles = new List<string> { "Manager", "DeputyManager", "AssistantManager", "Maker" };

            // Return only those users whose role is in the list but not equal to currentUserRole.
            return await _context.Users
                .Include(u => u.Role)
                .Where(u => managementRoles.Contains(u.Role.NameLT) &&
                            u.Role.NameLT.ToLower() != currentUserRole.ToLower())
                .Select(u => new BasicUserDto
                {
                    UserId = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    RoleLT = u.Role.NameLT,
                    RoleAR = u.Role.NameAR,
                })
                .ToListAsync();
        }
    }
}
