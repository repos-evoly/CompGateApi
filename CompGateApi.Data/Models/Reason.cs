using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("Reasons")]
    public class Reason
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string NameLT { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string NameAR { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Description { get; set; }
    }
}
