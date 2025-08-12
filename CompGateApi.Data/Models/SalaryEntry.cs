using System.ComponentModel.DataAnnotations.Schema;
public class SalaryEntry:Auditable
{
    public int Id { get; set; }
    public int SalaryCycleId { get; set; }
    public SalaryCycle SalaryCycle { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /* NEW ↓ – per-entry commission, net amounts, timestamps */
    [Column(TypeName = "decimal(18,2)")]
    public decimal CommissionAmount { get; set; }

    public bool IsTransferred { get; set; }
    public DateTime? TransferredAt { get; set; }
    public int? PostedByUserId { get; set; }

}
