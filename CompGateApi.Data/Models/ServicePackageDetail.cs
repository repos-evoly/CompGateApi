// CompGateApi.Data.Models/ServicePackageDetail.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    /// <summary>
    /// Links a ServicePackage to one TransactionCategory, 
    /// specifying commission (%) or fixed fee.
    /// </summary>
    [Table("ServicePackageDetails")]
    public class ServicePackageDetail : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ServicePackageId { get; set; }
        public ServicePackage ServicePackage { get; set; } = null!;

        [Required]
        public int TransactionCategoryId { get; set; }
        public TransactionCategory TransactionCategory { get; set; } = null!;

        /// <summary>Commission percentage (e.g. 0.5 = 0.5%).</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal CommissionPct { get; set; }

        /// <summary>Optional fixed fee per transaction.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal FeeFixed { get; set; }
    }
}
