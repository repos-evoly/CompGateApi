// CompGateApi.Data.Models/TransferLimit.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    public enum LimitPeriod
    {
        Daily,
        Weekly,
        Monthly
    }

    /// <summary>
    /// For a given ServicePackage & TransactionCategory & Currency & Period,
    /// defines min/max allowed transfer amounts.
    /// </summary>
    [Table("TransferLimits")]
    public class TransferLimit : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ServicePackageId { get; set; }
        public ServicePackage ServicePackage { get; set; } = null!;

        [Required]
        public int TransactionCategoryId { get; set; }
        public TransactionCategory TransactionCategory { get; set; } = null!;

        [Required]
        public int CurrencyId { get; set; }
        public Currency Currency { get; set; } = null!;

        [Required]
        public LimitPeriod Period { get; set; }

        /// <summary>Minimum single‐transaction amount.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal MinAmount { get; set; }

        /// <summary>Maximum aggregate over the given Period.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal MaxAmount { get; set; }
    }
}
