using System.Text.Json.Serialization;

public class EmployeeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public decimal Salary { get; set; }
    public decimal BankAllocationAmount { get; set; }
    public decimal EvoAllocationAmount { get; set; }
    public decimal BcdAllocationAmount { get; set; }
    public DateTime Date { get; set; }
    public string? AccountNumber { get; set; }
    public string AccountType { get; set; } = null!;
    public string? EvoWallet { get; set; }
    public string? EvoWalletRecipientType { get; set; }
    public string? BcdWallet { get; set; }
    public string? BcdWalletRecipientType { get; set; }
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
    public decimal BankAllocationAmount { get; set; }
    public decimal EvoAllocationAmount { get; set; }
    public decimal BcdAllocationAmount { get; set; }
    public DateTime Date { get; set; }
    public string? AccountNumber { get; set; }
    public string AccountType { get; set; } = "account";
    public string? EvoWallet { get; set; }
    public string? EvoWalletRecipientType { get; set; }
    public string? BcdWallet { get; set; }
    public string? BcdWalletRecipientType { get; set; }
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
    public string PostingStatus { get; set; } = "notPosted";
    public DateTimeOffset? PostingStartedAt { get; set; }
    public DateTimeOffset? PostingCompletedAt { get; set; }
    public bool? CanEdit { get; set; }
    public bool? CanPost { get; set; }
    public bool HasPendingWalletBatches { get; set; }
    public bool RequiresManualReview { get; set; }
    public bool HasPartialFailure { get; set; }
    public int CreatedByUserId { get; set; }
    public int? PostedByUserId { get; set; }
    public int EntryCount { get; set; }
    public List<SalaryEntryDto> Entries { get; set; } = new();

    public string? BankReference { get; set; }
    public string BankFeeStatus { get; set; } = "notRequired";
    public string? BankFeeError { get; set; }
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
    public decimal BankAllocationAmount { get; set; }
    public decimal EvoAllocationAmount { get; set; }
    public decimal BcdAllocationAmount { get; set; }
    public DateTime Date { get; set; }
    public string? AccountNumber { get; set; }
    public string AccountType { get; set; } = null!;
    public string? EvoWallet { get; set; }
    public string? EvoWalletRecipientType { get; set; }
    public string? BcdWallet { get; set; }
    public string? BcdWalletRecipientType { get; set; }
    public bool SendSalary { get; set; }
    public bool CanPost { get; set; }
    public bool IsDeleted { get; set; }

    /* ── payroll-specific data  ─────────────────────────────────────── */
    public bool IsTransferred { get; set; }
    public string? TransferResultCode { get; set; }
    public string? TransferResultReason { get; set; }
    public DateTime? TransferredAt { get; set; }
    public List<SalaryEntryAllocationDto> Allocations { get; set; } = new();
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
    public List<SalaryAllocationUpsertDto>? Allocations { get; set; }
}

public class SalaryAllocationUpsertDto
{
    public string PaymentChannel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class SalaryEntryAllocationDto
{
    public int Id { get; set; }
    public string PaymentChannel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string RecipientType { get; set; } = string.Empty;
    public string RecipientNameSnapshot { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsTransferred { get; set; }
    public decimal CommissionAmount { get; set; }
    public string? TransferResultCode { get; set; }
    public string? TransferResultReason { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderTxCode { get; set; }
    public DateTimeOffset? TransferredAt { get; set; }
    public string ReversalStatus { get; set; } = "notRequired";
    public string? ReversalReference { get; set; }
}

public class SalaryWalletBatchItemDto
{
    public int Id { get; set; }
    public int AllocationId { get; set; }
    public int RequestIndex { get; set; }
    public decimal Amount { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string RecipientType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal CommissionAmount { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderTxCode { get; set; }
}

public class SalaryWalletBatchDto
{
    public int Id { get; set; }
    public string Rail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CoreFundingStatus { get; set; } = string.Empty;
    public string? CoreFundingReference { get; set; }
    public string? ProviderBatchId { get; set; }
    public decimal RequestedTotalAmount { get; set; }
    public decimal SuccessfulTotalAmount { get; set; }
    public decimal FailedTotalAmount { get; set; }
    public decimal TotalCommission { get; set; }
    public decimal SenderCommission { get; set; }
    public decimal ReceiverCommission { get; set; }
    public decimal DebitAmount { get; set; }
    public bool CanRetry { get; set; }
    public bool OutcomeKnown { get; set; }
    public bool RequiresManualReview { get; set; }
    public bool CanRefreshStatus { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
    public string ReversalStatus { get; set; } = "notRequired";
    public decimal ReversalAmount { get; set; }
    public decimal ManualReconciliationAmount { get; set; }
    public string? ReversalReference { get; set; }
    public string? ReversalError { get; set; }
    public List<SalaryWalletBatchItemDto> Items { get; set; } = new();
}

public class SalaryPostingChannelSummaryDto
{
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int Pending { get; set; }
    public int Unknown { get; set; }
    public decimal Amount { get; set; }
    public decimal Commission { get; set; }
}

public class SalaryPostingSummaryDto
{
    public string OverallStatus { get; set; } = string.Empty;
    public SalaryPostingChannelSummaryDto BankAccount { get; set; } = new();
    public SalaryPostingChannelSummaryDto EvoWallet { get; set; } = new();
    public SalaryPostingChannelSummaryDto BcdWallet { get; set; } = new();
}

public class SalaryPostingResponseDto
{
    public SalaryCycleDto Cycle { get; set; } = null!;
    public SalaryPostingSummaryDto Summary { get; set; } = new();
}

public class SalaryWalletRailCapabilityDto
{
    public bool Enabled { get; set; }
    public bool Configured { get; set; }
    public bool Available { get; set; }
    public List<string> RecipientTypes { get; set; } = new();
}

public class SalaryWalletCapabilitiesDto
{
    public int AllocationPrecision { get; set; } = 3;
    public string CommissionMode { get; set; } = "receiverPaid";
    public Dictionary<string, SalaryWalletRailCapabilityDto> Rails { get; set; } = new();
    public bool CanCreateOrEdit { get; set; }
    public bool CanPost { get; set; }
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
    public int DeletedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<EmployeeExcelImportRowErrorDto> Errors { get; set; } = new();
}
