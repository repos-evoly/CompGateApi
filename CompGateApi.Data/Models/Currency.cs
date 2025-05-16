using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("Currencies")]
    public class Currency : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(3)]
        public string Code { get; set; } = string.Empty;

        public decimal Rate { get; set; }

        public string Description { get; set; } = string.Empty;
    }
}
