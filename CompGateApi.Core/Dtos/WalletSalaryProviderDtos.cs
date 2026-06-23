public class WalletSalaryTransferRequestDto
{
    public string BatchReference { get; set; } = string.Empty;
    public string CoreReferenceId { get; set; } = string.Empty;
    public string WalletChannel { get; set; } = string.Empty;
    public string Currency { get; set; } = "LYD";
    public decimal RequestedTotalAmount { get; set; }
    public List<WalletSalaryTransferItemDto> Items { get; set; } = new();
}

public class WalletSalaryTransferItemDto
{
    public string ClientReference { get; set; } = string.Empty;
    public int SalaryCycleId { get; set; }
    public int SalaryEntryId { get; set; }
    public int EmployeeId { get; set; }
    public string WalletId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "LYD";
}

public class WalletSalaryTransferResponseDto
{
    public string BatchReference { get; set; } = string.Empty;
    public string CoreReferenceId { get; set; } = string.Empty;
    public string WalletChannel { get; set; } = string.Empty;
    public string OverallStatus { get; set; } = string.Empty;
    public string Currency { get; set; } = "LYD";
    public decimal RequestedTotalAmount { get; set; }
    public decimal SuccessfulTotalAmount { get; set; }
    public decimal FailedTotalAmount { get; set; }
    public decimal TotalCommission { get; set; }
    public List<WalletSalaryTransferResultDto> Results { get; set; } = new();
    public string TraceId { get; set; } = string.Empty;
}

public class WalletSalaryStatusRequestDto
{
    public string BatchReference { get; set; } = string.Empty;
    public string CoreReferenceId { get; set; } = string.Empty;
    public string WalletChannel { get; set; } = string.Empty;
    public string Currency { get; set; } = "LYD";
    public List<WalletSalaryStatusItemDto> Items { get; set; } = new();
}

public class WalletSalaryStatusItemDto
{
    public string ClientReference { get; set; } = string.Empty;
    public int SalaryCycleId { get; set; }
    public int SalaryEntryId { get; set; }
    public int EmployeeId { get; set; }
}

public class WalletSalaryStatusResponseDto : WalletSalaryTransferResponseDto
{
}

public class WalletSalaryTransferResultDto
{
    public string ClientReference { get; set; } = string.Empty;
    public int SalaryCycleId { get; set; }
    public int SalaryEntryId { get; set; }
    public int EmployeeId { get; set; }
    public string WalletId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Commission { get; set; }
    public string Currency { get; set; } = "LYD";
    public string Status { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public string? ProviderTransactionId { get; set; }
    public DateTime ProcessedAt { get; set; }
}
