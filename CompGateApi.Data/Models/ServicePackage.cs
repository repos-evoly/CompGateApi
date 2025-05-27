// CompGateApi.Data.Models/ServicePackage.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    /// <summary>
    /// A named bundle of features & limits (e.g. Inquiry, Standard, Premium).
    /// Users subscribe to one package.
    /// </summary>
    [Table("ServicePackages")]
    public class ServicePackage : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Description { get; set; }

        /// <summary>
        /// Which transfer‐types this package allows + their commission/fee.
        /// </summary>
        public ICollection<ServicePackageDetail> ServicePackageDetails { get; set; }
            = new List<ServicePackageDetail>();

        /// <summary>
        /// The per‐period limits for each transfer‐type & currency for this package.
        /// </summary>
        public ICollection<TransferLimit> TransferLimits { get; set; }
            = new List<TransferLimit>();

        public ICollection<Company> Companies { get; set; }
            = new List<Company>();

        public ICollection<User> Users { get; set; }
             = new List<User>();
    }
}
