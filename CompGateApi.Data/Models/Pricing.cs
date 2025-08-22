using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("Pricing")]
    public class Pricing : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required, ForeignKey(nameof(TransactionCategory))]
        public int TrxCatId { get; set; }
        public TransactionCategory TransactionCategory { get; set; } = null!;

        [Column(TypeName = "decimal(18,4)")]
        public decimal? PctAmt { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? Price { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        // GL / account-ish fields
        [MaxLength(50)]
        public string? SGL1 { get; set; }

        [MaxLength(50)]
        public string? DGL1 { get; set; }

        [MaxLength(50)]
        public string? SGL2 { get; set; }

        [MaxLength(50)]
        public string? DGL2 { get; set; }

        // Transfer codes / accounts
        [MaxLength(50)]
        public string? DTC { get; set; }

        [MaxLength(50)]
        public string? CTC { get; set; }

        [MaxLength(50)]
        public string? DTC2 { get; set; }

        [MaxLength(50)]
        public string? CTC2 { get; set; }

        [MaxLength(500)]
        public string? NR2 { get; set; }

        [Required]
        public bool APPLYTR2 { get; set; } = false;
    }
}
