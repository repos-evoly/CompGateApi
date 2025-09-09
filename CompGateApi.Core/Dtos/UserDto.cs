using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using CompGateApi.Data.Models;

namespace CompGateApi.Core.Dtos
{
    public class UserDto
    {
        public int AuthUserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int RoleId { get; set; }
    }

    public class EditUserDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public bool IsActive { get; set; } = false;
    }

    public class PublicEditUserDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
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
        public int UserId { get; set; }  // local PK
        public int AuthUserId { get; set; }
        public string? Username { get; set; }  // from Auth
        public int? CompanyId { get; set; }  // six‐digit, optional
        public string? CompanyCode { get; set; }// the 6-digit code


        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        // instead of role object, you already return Role & RoleId:
        public RoleDto? Role { get; set; }
        public int RoleId { get; set; }

        public bool IsTwoFactorEnabled { get; set; }
        public string? PasswordResetToken { get; set; }
        public List<string> Permissions { get; set; } = new();  // existing
        public List<string> EnabledTransactionCategories { get; set; } = new();  // existing

        public List<string> Accounts { get; set; } = new();  // new
        public int ServicePackageId { get; set; }
        public bool IsCompanyAdmin { get; set; }
        public RegistrationStatus CompanyStatus { get; set; }
        public string? CompanyStatusMessage { get; set; }
        //  public int CompanyServicePackageId { get; set; }
        public bool IsActive { get; set; } = false;
    }

    public class BasicUserDto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string RoleLT { get; set; } = string.Empty;
        public string RoleAR { get; set; } = string.Empty;
    }

    public class AuthUserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty; // added
        public string FullNameAR { get; set; } = string.Empty;
        public string FullNameLT { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool Active { get; set; }
        public bool IsTwoFactorEnabled { get; set; }
        public string? PasswordResetToken { get; set; }

        [JsonPropertyName("lastLogin")]
        public DateTimeOffset? LastLogin { get; set; }

        [JsonPropertyName("lastLogout")]
        public DateTimeOffset? LastLogout { get; set; }
    }

    public class AuthRegisterResponseDto
    {
        public int userId { get; set; }
        public string? message { get; set; }
    }

    public class UserRegistrationDto
    {
        [Required, MaxLength(150)]
        public string Username { get; set; } = string.Empty;     // added

        [Required, MaxLength(150)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [StringLength(6, MinimumLength = 6)]
        public string? CompanyCode { get; set; }
        public string Phone { get; set; } = string.Empty;

        [Required]
        public int RoleId { get; set; }
    }


}
