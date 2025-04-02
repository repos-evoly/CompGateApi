using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace BlockingApi.Data.Models
{
    [Table("Sources")]
    public class Source:Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string NameLT { get; set; } = string.Empty; // ✅ Latin Name

        [Required]
        [MaxLength(100)]
        public string NameAR { get; set; } = string.Empty; // ✅ Arabic Name

        public ICollection<BlockRecord> BlockRecords { get; set; } = new List<BlockRecord>();
    }
}
