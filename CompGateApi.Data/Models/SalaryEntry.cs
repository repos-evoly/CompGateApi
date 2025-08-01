using System.ComponentModel.DataAnnotations.Schema;
public class SalaryEntry
{
    public int Id { get; set; }

    public int SalaryCycleId { get; set; }
    public SalaryCycle SalaryCycle { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public bool IsTransferred { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
