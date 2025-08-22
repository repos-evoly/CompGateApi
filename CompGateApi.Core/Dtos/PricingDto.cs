using System.ComponentModel.DataAnnotations;

namespace CompGateApi.Core.Dtos
{
    public class PricingDto
    {
        public int Id { get; set; }
        public int TrxCatId { get; set; }

        public decimal? PctAmt { get; set; }
        public decimal? Price { get; set; }
        public string? Description { get; set; }

        public string? SGL1 { get; set; }
        public string? DGL1 { get; set; }
        public string? SGL2 { get; set; }
        public string? DGL2 { get; set; }

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
        public string? Description { get; set; }

        public string? SGL1 { get; set; }
        public string? DGL1 { get; set; }
        public string? SGL2 { get; set; }
        public string? DGL2 { get; set; }

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
        public string? Description { get; set; }

        public string? SGL1 { get; set; }
        public string? DGL1 { get; set; }
        public string? SGL2 { get; set; }
        public string? DGL2 { get; set; }

        public string? DTC { get; set; }
        public string? CTC { get; set; }
        public string? DTC2 { get; set; }
        public string? CTC2 { get; set; }

        public string? NR2 { get; set; }
        public bool APPLYTR2 { get; set; }
    }
}
