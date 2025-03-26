using System.Collections.Generic;

namespace BlockingApi.Core.Dtos
{
    public class UserDto
    {
        public int AuthUserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public int BranchId { get; set; }
    }

    public class EditUserDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public int BranchId { get; set; }
    }

    public class AssignRoleDto
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
    }

    public class AssignUserPermissionsDto
    {
        public int UserId { get; set; }
        public List<UserPermissionAssignmentDto> Permissions { get; set; } = new();
    }

    public class UserPermissionAssignmentDto
    {
        public int PermissionId { get; set; }
        public int RoleId { get; set; }
    }

    public class PermissionStatusDto
    {
        public int PermissionId { get; set; }
        public string PermissionName { get; set; } = string.Empty;
        public int HasPermission { get; set; }  // 0 = No, 1 = Yes
    }


    public class UserDetailsDto
    {
        public int AuthUserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = "Unknown";
        public int RoleId { get; set; } = 0;
        public int BranchId { get; set; } = 0;
        public string Branch { get; set; } = "Unknown";
        public bool IsTwoFactorEnabled { get; set; }
        public string? PasswordResetToken { get; set; }
    }

    public class AuthUserDto
    {
        public int Id { get; set; }
        public string FullNameAR { get; set; } = string.Empty;
        public string FullNameLT { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool Active { get; set; }
        public bool IsTwoFactorEnabled { get; set; }
        public string? PasswordResetToken { get; set; }
    }

    public class AuthRegisterResponseDto
    {
        public int userId { get; set; }
        public string? message { get; set; }
    }


    public class UserRegistrationDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string  LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public int BranchId { get; set; }
        public string Phone { get; set; } = string.Empty;
    }

}
