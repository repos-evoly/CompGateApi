using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface IRoleRepository
    {
        Task<User> GetUserByAuthUserId(int authUserId); // Get user by AuthUserId
        Task<List<string>> GetUserPermissions(int userId);

        Task<List<RoleDto>> GetRolesAsync(bool? isGlobal = null);
        Task<RoleDto?> GetRoleByIdAsync(int roleId);
        Task<RoleDto> CreateRoleAsync(string nameLT, string nameAR, string description, bool isGlobal);
        Task<bool> UpdateRoleAsync(int roleId, string nameLT, string nameAR, string description, bool isGlobal);
        Task<bool> DeleteRoleAsync(int roleId);

        Task<List<PermissionDto>> GetAllPermissionsAsync();
        Task<List<PermissionDto>> GetPermissionsByRoleAsync(int roleId);
        Task<List<PermissionDto>> GetPermissionsByGlobalAsync(bool isGlobal);

        Task<bool> AssignPermissionsToRoleAsync(int roleId, IEnumerable<int> permissionIds);

        Task<PermissionDto> CreatePermissionAsync(string nameAr, string nameEn, string description, bool isGlobal, string? type);
        Task<bool> UpdatePermissionAsync(int id, string nameAr, string nameEn, string description, bool isGlobal, string? type);
        Task<bool> DeletePermissionAsync(int id);
    }
}
