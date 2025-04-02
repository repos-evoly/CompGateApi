using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlockingApi.Data.Models
{
    [Table("Branches")]
    public class Branch:Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(10)]
        public string CABBN { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Address { get; set; } = string.Empty;

        [MaxLength(15)]
        public string Phone { get; set; } = string.Empty;

        public int AreaId { get; set; }
        public Area Area { get; set; } = null!;

        public int? BranchManagerId { get; set; }
        public User? BranchManager { get; set; }

        public ICollection<User> Employees { get; set; } = new List<User>();
    }
}
