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
    public string? EvoWallet { get; set; }
    public string? BcdWallet { get; set; }
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
    public string? EvoWallet { get; set; }
    public string? BcdWallet { get; set; }
    public bool SendSalary { get; set; }
    public bool CanPost { get; set; }
}


public class SalaryCycleDto
{
    public int Id { get; set; }
    public string SalaryMonth { get; set; } = string.Empty;
    public string? AdditionalMonth { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public string DebitAccount { get; set; } = null!;
    public string Currency { get; set; } = "LYD";
    public DateTime? PostedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public int? PostedByUserId { get; set; }
    public List<SalaryEntryDto> Entries { get; set; } = new();

    public string? BankReference { get; set; }
    public string? BankResponseRaw { get; set; }
    public string? BankBatchHistoryJson { get; set; }
}

public class SalaryCycleCreateDto
{
    public string SalaryMonth { get; set; } = string.Empty;
    public int? AdditionalMonth { get; set; }
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
    public string? TransferResultCode { get; set; }
    public string? TransferResultReason { get; set; }
    public DateTime? TransferredAt { get; set; }
}


public class SalaryCycleSaveDto
{
    public string? SalaryMonth { get; set; }   // optional
    public int? AdditionalMonth { get; set; }   // optional, persisted as string
    public string? DebitAccount { get; set; }   // optional
    public string? Currency { get; set; }   // optional (e.g. "LYD")

    public List<SalaryEntryUpsertDto> Entries { get; set; } = new();
}

public class SalaryCycleAddEntriesDto
{
    public List<SalaryEntryUpsertDto> Entries { get; set; } = new();
}

public class SalaryRepostItemDto
{
    public int EntryId { get; set; }
    public decimal? NewAmount { get; set; }
}

public class SalaryRepostRequestDto
{
    public List<SalaryRepostItemDto> Items { get; set; } = new();
}

public class SalaryRepostIdsRequestDto
{
    public List<int> EntryIds { get; set; } = new();
    public List<int> EmployeeIds { get; set; } = new();
}

public class SalaryEntryEditDto
{
    public decimal? Amount { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountType { get; set; }
    public string? EvoWallet { get; set; }
    public string? BcdWallet { get; set; }
}

public class SalaryEntryUpsertDto
{
    public int EmployeeId { get; set; }
    public decimal Salary { get; set; }
}

public class SalaryCycleAdminListItemDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;

    public string SalaryMonth { get; set; } = string.Empty;
    public string? AdditionalMonth { get; set; }
    public string DebitAccount { get; set; } = string.Empty;
    public string Currency { get; set; } = "LYD";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public int? PostedByUserId { get; set; }
    public decimal TotalAmount { get; set; }

    public string? BankReference { get; set; }

    public string? BankFeeReference { get; set; }
    public string? BankBatchHistoryJson { get; set; }

}

public class SalaryCycleAdminDetailDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;

    public string SalaryMonth { get; set; } = string.Empty;
    public string? AdditionalMonth { get; set; }
    public string DebitAccount { get; set; } = string.Empty;
    public string Currency { get; set; } = "LYD";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public int? PostedByUserId { get; set; }
    public decimal TotalAmount { get; set; }

    public string? BankReference { get; set; }
    public string? BankResponseRaw { get; set; }

    public string? BankFeeReference { get; set; }
    public string? BankFeeResponseRaw { get; set; }
    public string? BankBatchHistoryJson { get; set; }

    public List<SalaryEntryDto> Entries { get; set; } = new();
}

public class EmployeeExcelImportRowErrorDto
{
    public int RowNumber { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class EmployeeExcelImportResultDto
{
    public int TotalRows { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<EmployeeExcelImportRowErrorDto> Errors { get; set; } = new();
}
