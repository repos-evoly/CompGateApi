using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CompGateApi.Data.Models;

public class SalaryWalletBatch : Auditable
{
    public int Id { get; set; }

    public int SalaryCycleId { get; set; }
    public SalaryCycle SalaryCycle { get; set; } = null!;

    [MaxLength(20)]
    public string WalletChannel { get; set; } = string.Empty;

    [MaxLength(34)]
    public string ShadowAccount { get; set; } = string.Empty;

    [MaxLength(64)]
    public string BatchReference { get; set; } = string.Empty;

    [MaxLength(64)]
    public string CoreReferenceId { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,3)")]
    public decimal RequestedTotalAmount { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal SuccessfulTotalAmount { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal FailedTotalAmount { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal TotalCommission { get; set; }

    [MaxLength(20)]
    public string OverallStatus { get; set; } = "pending";

    [Column(TypeName = "nvarchar(max)")]
    public string? ProviderRequestJson { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ProviderResponseJson { get; set; }

    [MaxLength(1024)]
    public string? ProviderErrorMessage { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public bool ReversalRequired { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal ReversalAmount { get; set; }

    [MaxLength(20)]
    public string ReversalStatus { get; set; } = "not_required";

    [MaxLength(64)]
    public string? ReversalBankReference { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ReversalRequestJson { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ReversalResponseJson { get; set; }

    [MaxLength(1024)]
    public string? ReversalErrorMessage { get; set; }

    public DateTime? ReversedAt { get; set; }
}
