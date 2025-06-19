using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    // CompGateApi.Data.Models/ServicePackageDetail.cs
    [Table("ServicePackageDetails")]
    public class ServicePackageDetail : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required, ForeignKey(nameof(ServicePackage))]
        public int ServicePackageId { get; set; }
        public ServicePackage ServicePackage { get; set; } = null!;

        [Required, ForeignKey(nameof(TransactionCategory))]
        public int TransactionCategoryId { get; set; }
        public TransactionCategory TransactionCategory { get; set; } = null!;

        /// <summary>Enable/disable category for this package</summary>
        [Required]
        public bool IsEnabledForPackage { get; set; }

        // make all of these nullable:
        public decimal? B2BTransactionLimit { get; set; }
        public decimal? B2CTransactionLimit { get; set; }
        public decimal? B2BFixedFee { get; set; }
        public decimal? B2CFixedFee { get; set; }
        public decimal? B2BMinPercentage { get; set; }
        public decimal? B2CMinPercentage { get; set; }
        public decimal? B2BCommissionPct { get; set; }
        public decimal? B2CCommissionPct { get; set; }
    }

}