using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("Roles")]
    public class Role : Auditable
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(100)]
        public string NameAR { get; set; } = string.Empty;
        public string NameLT { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsGlobal { get; set; } = false;

        public ICollection<User> Users { get; set; } = new List<User>();

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
        public ICollection<UserRolePermission> UserRolePermissions { get; set; } = new List<UserRolePermission>();

    }
}
