public class EmployeeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public decimal Salary { get; set; }
    public DateTime Date { get; set; }
    public string AccountNumber { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public bool SendSalary { get; set; }
    public bool CanPost { get; set; }
}

public class EmployeeCreateDto
{
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public decimal Salary { get; set; }
    public DateTime Date { get; set; }
    public string AccountNumber { get; set; } = null!;
    public string AccountType { get; set; } = "account";
    public bool SendSalary { get; set; }
    public bool CanPost { get; set; }
}


public class SalaryCycleDto
{
    public int Id { get; set; }
    public DateTime SalaryMonth { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public int? PostedByUserId { get; set; }
    public List<SalaryEntryDto> Entries { get; set; } = new();
}

public class SalaryCycleCreateDto
{
    public DateTime SalaryMonth { get; set; }
    public List<int> EmployeeIds { get; set; } = new();
}


public class SalaryEntryDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = null!;
    public decimal Amount { get; set; }
    public bool IsTransferred { get; set; }
}
