using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CompGateApi.Data.Models;

namespace CompGateApi.Data.Models
{
    [Table("Documents")]
    public class Document : Auditable
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(100)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FileMimeType { get; set; } = string.Empty;

        [Required]
        public int FileSize { get; set; }

        [Required]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        public string DocumentType { get; set; } = "reports";

        [Required]
        public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.Now;

        [Required]
        public int UploadedByUserId { get; set; }

        [ForeignKey(nameof(UploadedByUserId))]
        public User UploadedBy { get; set; } = null!;
    }
}
