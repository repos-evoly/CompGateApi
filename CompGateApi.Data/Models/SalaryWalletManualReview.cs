using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class SalaryWalletManualReview : Auditable
{
    public int Id { get; set; }

    public int SalaryWalletBatchId { get; set; }
    public SalaryWalletBatch SalaryWalletBatch { get; set; } = null!;

    public int SalaryCycleId { get; set; }
    public SalaryCycle SalaryCycle { get; set; } = null!;

    [MaxLength(20)]
    public string WalletChannel { get; set; } = string.Empty;

    [MaxLength(64)]
    public string BatchReference { get; set; } = string.Empty;

    [MaxLength(64)]
    public string CoreReferenceId { get; set; } = string.Empty;

    [MaxLength(34)]
    public string ShadowAccount { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,3)")]
    public decimal RequestedAmount { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal UnresolvedAmount { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = "open";

    [MaxLength(64)]
    public string ReasonCode { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string ReasonMessage { get; set; } = string.Empty;

    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }

    [MaxLength(1024)]
    public string? LastErrorMessage { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ProviderRequestJson { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ProviderResponseJson { get; set; }

    public DateTime? ResolvedAt { get; set; }

    [MaxLength(1024)]
    public string? ResolutionNote { get; set; }
}
