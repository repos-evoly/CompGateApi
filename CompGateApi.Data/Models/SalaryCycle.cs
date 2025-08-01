using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CompGateApi.Data.Models;
public class SalaryCycle
{
    public int Id { get; set; }

    [Required]
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public DateTime SalaryMonth { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int CreatedByUserId { get; set; } // saved by
    public int? PostedByUserId { get; set; } // posted by

    public DateTime? PostedAt { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    public ICollection<SalaryEntry> Entries { get; set; } = new List<SalaryEntry>();
}

