using System.Text.Json.Serialization;

public class EmployeeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public decimal Salary { get; set; }
    public DateTime Date { get; set; }
    public string AccountNumber { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public string? EvoWallet { get; set; }
    public string? BcdWallet { get; set; }
    public decimal AccountAllocationAmount { get; set; }
    public decimal EvoAllocationAmount { get; set; }
    public decimal BcdAllocationAmount { get; set; }
    public bool SendSalary { get; set; }
    public bool CanPost { get; set; }
    public bool IsDeleted { get; set; }
}

public class EmployeeCreateDto
{
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public decimal Salary { get; set; }
    public DateTime Date { get; set; }
    public string AccountNumber { get; set; } = null!;
    public string AccountType { get; set; } = "account";
    public string? EvoWallet { get; set; }
    public string? BcdWallet { get; set; }
    public decimal AccountAllocationAmount { get; set; }
    public decimal EvoAllocationAmount { get; set; }
    public decimal BcdAllocationAmount { get; set; }
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
    public int EntryCount { get; set; }
    public List<SalaryEntryDto> Entries { get; set; } = new();

    public string? BankReference { get; set; }
    [JsonIgnore]
    public string? BankResponseRaw { get; set; }
    [JsonIgnore]
    public string? BankBatchHistoryJson { get; set; }
    public List<SalaryWalletBatchDto> WalletBatches { get; set; } = new();
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
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public decimal Salary { get; set; }
    public DateTime Date { get; set; }
    public string AccountNumber { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public string? EvoWallet { get; set; }
    public string? BcdWallet { get; set; }
    public bool SendSalary { get; set; }
    public bool CanPost { get; set; }
    public bool IsDeleted { get; set; }

    /* ── payroll-specific data  ─────────────────────────────────────── */
    public bool IsTransferred { get; set; }
    public string? TransferResultCode { get; set; }
    public string? TransferResultReason { get; set; }
    public DateTime? TransferredAt { get; set; }
    public decimal CommissionAmount { get; set; }
    public List<SalaryEntryAllocationDto> Allocations { get; set; } = new();
}

public class SalaryEntryAllocationDto
{
    public int Id { get; set; }
    public int SalaryEntryId { get; set; }
    public string PaymentChannel { get; set; } = "account";
    public decimal Amount { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string ClientReference { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? TransferResultCode { get; set; }
    public string? TransferResultReason { get; set; }
    public string? ProviderTransactionId { get; set; }
    public decimal CommissionAmount { get; set; }
    public bool IsTransferred { get; set; }
    public DateTime? TransferredAt { get; set; }
}

public class SalaryWalletBatchDto
{
    public int Id { get; set; }
    public int SalaryCycleId { get; set; }
    public int? PostedByUserId { get; set; }
    public string WalletChannel { get; set; } = string.Empty;
    public string ShadowAccount { get; set; } = string.Empty;
    public string BatchReference { get; set; } = string.Empty;
    public string CoreReferenceId { get; set; } = string.Empty;
    public decimal RequestedTotalAmount { get; set; }
    public decimal SuccessfulTotalAmount { get; set; }
    public decimal FailedTotalAmount { get; set; }
    public decimal TotalCommission { get; set; }
    public string OverallStatus { get; set; } = "pending";
    public string ReconciliationStatus { get; set; } = "not_required";
    public string ReconciliationMode { get; set; } = "payment_retry";
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public string? LastErrorMessage { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string ReversalStatus { get; set; } = "not_required";
    public decimal ReversalAmount { get; set; }
    public string? ReversalBankReference { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? ReversedAt { get; set; }
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
    public List<SalaryEntryAllocationUpsertDto>? Allocations { get; set; }
}

public class SalaryEntryAllocationUpsertDto
{
    public string PaymentChannel { get; set; } = "account";
    public decimal Amount { get; set; }
    public string? Destination { get; set; }
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
    public List<SalaryWalletBatchDto> WalletBatches { get; set; } = new();
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
    public int DeletedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<EmployeeExcelImportRowErrorDto> Errors { get; set; } = new();
}
