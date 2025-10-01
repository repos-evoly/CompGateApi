using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CompGateApi.Data.Models;
public class SalaryCycle : Auditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public DateTime SalaryMonth { get; set; }
    public int CreatedByUserId { get; set; }

    /* NEW ↓ */
    [MaxLength(34)] public string DebitAccount { get; set; } = string.Empty;
    [MaxLength(3)] public string Currency { get; set; } = "LYD";

    public int? PostedByUserId { get; set; }
    public DateTime? PostedAt { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [MaxLength(32)]
    public string? BankReference { get; set; }       // HID/referenceId sent to bank

    [Column(TypeName = "nvarchar(max)")]
    public string? BankResponseRaw { get; set; }

    [MaxLength(32)] public string? BankFeeReference { get; set; }
    public string? BankFeeResponseRaw { get; set; }

    public ICollection<SalaryEntry> Entries { get; set; } = new List<SalaryEntry>();
}


