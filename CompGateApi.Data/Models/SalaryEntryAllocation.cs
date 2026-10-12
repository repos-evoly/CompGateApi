using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class SalaryEntryAllocation : Auditable
{
    public int Id { get; set; }

    public int SalaryEntryId { get; set; }
    public SalaryEntry SalaryEntry { get; set; } = null!;

    [MaxLength(24)]
    public string PaymentChannel { get; set; } = "bankAccount";

    [Column(TypeName = "decimal(18,3)")]
    public decimal Amount { get; set; }

    [MaxLength(34)]
    public string Destination { get; set; } = string.Empty;

    [MaxLength(20)]
    public string RecipientType { get; set; } = "accountNumber";

    [MaxLength(200)]
    public string RecipientNameSnapshot { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Status { get; set; } = "pending";

    public bool IsTransferred { get; set; }
    public DateTimeOffset? TransferredAt { get; set; }
    public int? PostedByUserId { get; set; }

    [MaxLength(64)]
    public string? TransferResultCode { get; set; }

    [MaxLength(1024)]
    public string? TransferResultReason { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal CommissionAmount { get; set; }

    [MaxLength(128)]
    public string? ProviderTransactionId { get; set; }

    [MaxLength(64)]
    public string? ProviderTxCode { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ProviderResponseRaw { get; set; }

    [MaxLength(32)]
    public string ReversalStatus { get; set; } = "notRequired";

    [MaxLength(64)]
    public string? ReversalReference { get; set; }

    [MaxLength(1024)]
    public string? ReversalError { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<SalaryWalletBatchItem> WalletBatchItems { get; set; } =
        new List<SalaryWalletBatchItem>();
}
