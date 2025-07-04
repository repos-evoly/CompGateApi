// CompAuthApi.Data.Models/FormStatus.cs
using System.ComponentModel.DataAnnotations;

namespace CompGateApi.Data.Models
{
    public class FormStatus : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string NameEn { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string NameAr { get; set; } = null!;

        [MaxLength(250)]
        public string? DescriptionEn { get; set; }

        [MaxLength(250)]
        public string? DescriptionAr { get; set; }
    }
}
