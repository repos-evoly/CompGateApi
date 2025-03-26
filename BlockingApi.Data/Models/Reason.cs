using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace BlockingApi.Data.Models
{
    [Table("Reasons")]
    public class Reason
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(250)]
        public string NameLT { get; set; } = string.Empty; // ✅ Latin Name

        [Required]
        [MaxLength(250)]
        public string NameAR { get; set; } = string.Empty; // ✅ Arabic Name

        public ICollection<BlockRecord> BlockRecords { get; set; } = new List<BlockRecord>();
    }
}
