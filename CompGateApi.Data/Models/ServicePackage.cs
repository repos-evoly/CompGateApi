using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("ServicePackages")]
    public class ServicePackage : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        

        /// <summary>
        /// Cap per day (all categories combined)
        /// </summary>
        [Required]
        public decimal DailyLimit { get; set; }

        /// <summary>
        /// Cap per month (all categories combined)
        /// </summary>
        [Required]
        public decimal MonthlyLimit { get; set; }

        /// <summary>
        /// Per-category details: limits, fees, enable flags
        /// </summary>
        public ICollection<ServicePackageDetail> Details { get; set; }
            = new List<ServicePackageDetail>();

        /// <summary>
        /// Companies assigned this package
        /// </summary>
        public ICollection<Company> Companies { get; set; } = new List<Company>();
    }
}