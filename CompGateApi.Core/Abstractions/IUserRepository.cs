using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CompGateApi.Core.Abstractions
{
    public interface IUserRepository
    {
        Task<bool> AddUser(User user);
        Task<bool> AssignRole(int userId, int roleId);
        Task<bool> AssignUserPermissions(int userId, List<UserPermissionAssignmentDto> permissions);

        Task<bool> RemoveRolePermissions(int userId);
        Task<List<UserDetailsDto>> GetUsersAsync(
              string? searchTerm,
              string? searchBy,
              bool? hasCompany,
              int? roleId,
              int page,
              int limit,
              string authToken
          ); Task<int> GetUserCountAsync(string? searchTerm, string? searchBy);
        Task<UserDetailsDto?> GetUserById(int userId, string authToken);
        Task<UserDetailsDto?> GetUserByAuthId(int userId, string authToken);
        Task<List<PermissionStatusDto>> GetUserPermissions(int userId);
        Task<bool> EditUser(int userId, EditUserDto editUserDto);
        Task<List<RoleDto>> GetRolesAsync(bool? isGlobal = null);
        Task<List<Permission>> GetPermissions();

        Task<int> GetUsersCountAsync(
            string? searchTerm,
            string? searchBy,
            bool? hasCompany,
            int? roleId
        );

        Task<List<Permission>> GetPermissionsByRoleAsync(int roleId);

        Task<List<Permission>> GetPermissionsByGlobalAsync(bool isGlobal);

        Task<List<BasicUserDto>> GetManagementUsersAsync(string currentUserRole);


    }
}
