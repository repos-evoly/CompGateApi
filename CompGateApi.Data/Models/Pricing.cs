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

        /// <summary>
        /// Flexible rule:
        /// - If set to a numeric string (e.g. "20") → use that as the price.
        /// - If set to "amount" → take amount from the request instead of fixed.
        /// - If null/empty → fallback to Price.
        /// </summary>
        [MaxLength(50)]
        public string? AmountRule { get; set; }

        /// <summary>Unit size (e.g., 24 or 48 for checkbooks). Defaults to 1 when not unitized.</summary>
        [Required]
        public int Unit { get; set; } = 1;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(50)] public string? GL1 { get; set; }
        [MaxLength(50)] public string? GL2 { get; set; }
        [MaxLength(50)] public string? GL3 { get; set; }
        [MaxLength(50)] public string? GL4 { get; set; }

        // Transfer codes (mapped to @DTCD/@CTCD in the CompanyGatewayPostTransfer payload)
        [MaxLength(50)] public string? DTC { get; set; }   // -> @DTCD
        [MaxLength(50)] public string? CTC { get; set; }   // -> @CTCD
        [MaxLength(50)] public string? DTC2 { get; set; }  // -> @DTCD2
        [MaxLength(50)] public string? CTC2 { get; set; }  // -> @CTCD2

        // Narrative for the transfer; falls back to endpoint-provided description
        [MaxLength(500)]
        public string? NR2 { get; set; }

        /// <summary>If true, second leg is applied; otherwise APLYTRN2="N" and TRFAMT2=0.</summary>
        [Required]
        public bool APPLYTR2 { get; set; } = false;
    }
}
