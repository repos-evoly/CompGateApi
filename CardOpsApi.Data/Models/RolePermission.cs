using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardOpsApi.Data.Models
{
    [Table("RolePermissions")]
    public class RolePermission : Auditable
    {
        [Key]
        public int Id { get; set; }

        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        public int PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;
    }
}
