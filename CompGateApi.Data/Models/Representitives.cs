// CompAuthApi.Data.Models/Representative.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CompGateApi.Data.Models;

namespace CompGateApi.Data.Models
{
    public class Representative : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Number { get; set; } = null!;          

        [Required]
        [MaxLength(50)]
        public string PassportNumber { get; set; } = null!;   

         public bool IsActive { get; set; } = true;


        // foreign key to Company
        [ForeignKey(nameof(Company))]
        public int CompanyId { get; set; }

        // navigation
        public Company Company { get; set; } = null!;
    }
}
