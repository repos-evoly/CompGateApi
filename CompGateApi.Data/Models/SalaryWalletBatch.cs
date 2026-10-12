using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class SalaryWalletBatch : Auditable
{
    public int Id { get; set; }

    public int SalaryCycleId { get; set; }
    public SalaryCycle SalaryCycle { get; set; } = null!;

    [MaxLength(16)]
    public string Rail { get; set; } = string.Empty;

    public int SequenceNumber { get; set; } = 1;

    [MaxLength(32)]
    public string Purpose { get; set; } = "initial";

    [MaxLength(34)]
    public string ShadowAccount { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Status { get; set; } = "prepared";

    [MaxLength(32)]
    public string CoreFundingStatus { get; set; } = "notStarted";

    [MaxLength(64)]
    public string? CoreFundingReference { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? CoreFundingResponseRaw { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal RequestedTotalAmount { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal SuccessfulTotalAmount { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal FailedTotalAmount { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal TotalCommission { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal SenderCommission { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal ReceiverCommission { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal DebitAmount { get; set; }

    [MaxLength(128)]
    public string? ProviderBatchId { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string ProviderRequestJson { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ProviderRequestHash { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string? ProviderResponseJson { get; set; }

    [MaxLength(32)]
    public string DispatchClassification { get; set; } = "notDispatched";

    public bool RetryAllowed { get; set; }

    [MaxLength(64)]
    public string? LastErrorCode { get; set; }

    [MaxLength(1024)]
    public string? LastError { get; set; }

    public DateTimeOffset? DispatchStartedAt { get; set; }
    public DateTimeOffset? DispatchCompletedAt { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
    public DateTimeOffset? NextCheckAt { get; set; }
    public DateTimeOffset? CheckClaimedAt { get; set; }

    [MaxLength(32)]
    public string ReversalStatus { get; set; } = "notRequired";

    [Column(TypeName = "decimal(18,3)")]
    public decimal ReversalAmount { get; set; }

    [MaxLength(64)]
    public string? ReversalReference { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ReversalResponseRaw { get; set; }

    [MaxLength(1024)]
    public string? ReversalError { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<SalaryWalletBatchItem> Items { get; set; } =
        new List<SalaryWalletBatchItem>();

    public ICollection<SalaryWalletBatchAttempt> Attempts { get; set; } =
        new List<SalaryWalletBatchAttempt>();

    public ICollection<SalaryWalletManualReview> ManualReviews { get; set; } =
        new List<SalaryWalletManualReview>();
}
