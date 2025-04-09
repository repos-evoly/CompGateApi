using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CardOpsApi.Data.Models
{
    [Table("Users")]
    [Index(nameof(Email), IsUnique = true, Name = "Unique_Email")]
    public class User : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AuthUserId { get; set; }

        [MaxLength(150)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(15)]
        public string Phone { get; set; } = string.Empty;

        public int RoleId { get; set; } // FK to Role table
        public Role Role { get; set; } = null!;

        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
        public ICollection<UserRolePermission> UserRolePermissions { get; set; } = new List<UserRolePermission>();

    }
}
