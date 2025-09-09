using System.ComponentModel.DataAnnotations;

namespace CompGateApi.Core.Dtos
{
    public class PricingDto
    {
        public int Id { get; set; }
        public int TrxCatId { get; set; }

        public decimal? PctAmt { get; set; }
        public decimal? Price { get; set; }

        /// <summary>
        /// "amount" to take from request; or any numeric string like "20" to force fixed price.
        /// If null/empty, consumers should fallback to Price.
        /// </summary>
        public string? AmountRule { get; set; }

        public int Unit { get; set; } = 1;
        public string? Description { get; set; }

        public string? GL1 { get; set; }
        public string? GL2 { get; set; }
        public string? GL3 { get; set; }
        public string? GL4 { get; set; }

        public string? DTC { get; set; }
        public string? CTC { get; set; }
        public string? DTC2 { get; set; }
        public string? CTC2 { get; set; }

        public string? NR2 { get; set; }
        public bool APPLYTR2 { get; set; }
    }

    public class PricingCreateDto
    {
        [Required]
        public int TrxCatId { get; set; }

        public decimal? PctAmt { get; set; }
        public decimal? Price { get; set; }

        /// <summary>
        /// "amount" to take from request; or any numeric string like "20".
        /// Optional; if omitted, consumers fallback to Price.
        /// </summary>
        [MaxLength(50)]
        public string? AmountRule { get; set; }

        public int Unit { get; set; } = 1;
        public string? Description { get; set; }

        public string? GL1 { get; set; }
        public string? GL2 { get; set; }
        public string? GL3 { get; set; }
        public string? GL4 { get; set; }

        public string? DTC { get; set; }
        public string? CTC { get; set; }
        public string? DTC2 { get; set; }
        public string? CTC2 { get; set; }

        public string? NR2 { get; set; }
        public bool APPLYTR2 { get; set; } = false;
    }

    public class PricingUpdateDto
    {
        [Required]
        public int TrxCatId { get; set; }

        public decimal? PctAmt { get; set; }
        public decimal? Price { get; set; }

        /// <summary>
        /// "amount" to take from request; or any numeric string like "20".
        /// Optional; if omitted, consumers fallback to Price.
        /// </summary>
        [MaxLength(50)]
        public string? AmountRule { get; set; }

        public int Unit { get; set; } = 1;
        public string? Description { get; set; }

        public string? GL1 { get; set; }
        public string? GL2 { get; set; }
        public string? GL3 { get; set; }
        public string? GL4 { get; set; }

        public string? DTC { get; set; }
        public string? CTC { get; set; }
        public string? DTC2 { get; set; }
        public string? CTC2 { get; set; }

        public string? NR2 { get; set; }
        public bool APPLYTR2 { get; set; }
    }
}
