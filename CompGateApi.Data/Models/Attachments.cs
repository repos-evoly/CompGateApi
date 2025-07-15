using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
  [Table("Attachment")]
  public class Attachment : Auditable
  {
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
    [MaxLength(30)]
    public string AttSubject { get; set; } = string.Empty;

    [MaxLength(100)]
    public string AttFileName { get; set; } = string.Empty;
    [MaxLength(100)]
    public string AttOriginalFileName { get; set; } = string.Empty;
    [MaxLength(100)]
    public string AttMime { get; set; } = string.Empty;
    public int AttSize { get; set; }
    public string AttUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [MaxLength(30)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(8)]
    public int CompanyId { get; set; }

    [ForeignKey(nameof(CompanyId))]
    public Company Company { get; set; } = null!;

    public int? CblRequestId { get; set; }
    [ForeignKey(nameof(CblRequestId))]
    public CblRequest? CblRequest { get; set; }

    // ← NEW: link back to exactly one Visa request (or null)
    public int? VisaRequestId { get; set; }
    [ForeignKey(nameof(VisaRequestId))]
    public VisaRequest? VisaRequest { get; set; }
  }
}