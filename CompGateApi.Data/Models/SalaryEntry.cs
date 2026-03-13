using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
public class SalaryEntry:Auditable
{
    public int Id { get; set; }
    public int SalaryCycleId { get; set; }
    public SalaryCycle SalaryCycle { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    [Column(TypeName = "decimal(18,3)")]
    public decimal Amount { get; set; }

    /* NEW ↓ – per-entry commission, net amounts, timestamps */
    [Column(TypeName = "decimal(18,3)")]
    public decimal CommissionAmount { get; set; }

    public bool IsTransferred { get; set; }
    public DateTime? TransferredAt { get; set; }
    public int? PostedByUserId { get; set; }

    // Per-entry bank response details
    [MaxLength(32)]
    public string? TransferResultCode { get; set; }
    [MaxLength(1024)]
    public string? TransferResultReason { get; set; }
    [Column(TypeName = "nvarchar(max)")]
    public string? BankLineResponseRaw { get; set; }

    // No per-entry override fields; edits are applied to Employee

}
