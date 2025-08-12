using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("TransactionCategories")]
    public class TransactionCategory : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        public bool HasLimits { get; set; } = false;
        public bool CountsTowardTxnLimits { get; set; } = true;
        public ICollection<ServicePackageDetail> PackageDetails { get; set; }
= new List<ServicePackageDetail>();
    }
}