using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CompGateApi.Data.Models;

public class Employee
{
    public int Id { get; set; }

    [Required]
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = null!;

    [MaxLength(100)]
    public string Email { get; set; } = null!;

    [MaxLength(20)]
    public string Phone { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Salary { get; set; }

    public DateTime Date { get; set; }

    [MaxLength(34)]
    public string AccountNumber { get; set; } = null!;

    [MaxLength(20)]
    public string AccountType { get; set; } = "account"; // or "wallet"

    public bool SendSalary { get; set; }
    public bool CanPost { get; set; }

    public ICollection<SalaryEntry> SalaryEntries { get; set; } = new List<SalaryEntry>();
}
