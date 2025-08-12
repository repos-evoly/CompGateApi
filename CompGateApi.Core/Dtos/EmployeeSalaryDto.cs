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
    public DateTimeOffset CreatedAt { get; set; }

    public string DebitAccount { get; set; } = null!;
    public string Currency { get; set; } = "LYD";
    public DateTime? PostedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public int? PostedByUserId { get; set; }
    public List<SalaryEntryDto> Entries { get; set; } = new();
}

public class SalaryCycleCreateDto
{
    public DateTime SalaryMonth { get; set; }
    public string DebitAccount { get; set; } = null!;
    public string Currency { get; set; } = "LYD";
    public List<SalaryEntryUpsertDto>? Entries { get; set; }
}


public class SalaryEntryDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }

    /* ── all the fields that EmployeeDto exposes ───────────────────── */
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public decimal Salary { get; set; }
    public DateTime Date { get; set; }
    public string AccountNumber { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public bool SendSalary { get; set; }
    public bool CanPost { get; set; }

    /* ── payroll-specific data  ─────────────────────────────────────── */
    public bool IsTransferred { get; set; }
}


public class SalaryCycleSaveDto
{
    public DateTime? SalaryMonth { get; set; }   // optional
    public string? DebitAccount { get; set; }   // optional
    public string? Currency { get; set; }   // optional (e.g. "LYD")

    public List<SalaryEntryUpsertDto> Entries { get; set; } = new();
}

public class SalaryEntryUpsertDto
{
    public int EmployeeId { get; set; }
    public decimal Salary { get; set; }
}