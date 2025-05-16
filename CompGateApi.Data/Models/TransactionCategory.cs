// CompGateApi.Data.Models/TransactionCategory.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    /// <summary>
    /// A transfer type: Internal(same bank), External(inter-bank), International, RTGS, etc.
    /// Used for routing, pricing, and per‐period limiting.
    /// </summary>
    [Table("TransactionCategories")]
    public class TransactionCategory : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Description { get; set; }

        public ICollection<ServicePackageDetail> ServicePackageDetails { get; set; }
            = new List<ServicePackageDetail>();

        public ICollection<TransferLimit> TransferLimits { get; set; }
            = new List<TransferLimit>();
    }
}
