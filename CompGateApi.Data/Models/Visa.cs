// CompGateApi.Data.Models/Visa.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("Visas")]
    public class Visa : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string NameEn { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string NameAr { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [MaxLength(500)]
        public string? DescriptionEn { get; set; }

        [MaxLength(500)]
        public string? DescriptionAr { get; set; }

        // Admin-owned assets (no company)
        public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    }
}
