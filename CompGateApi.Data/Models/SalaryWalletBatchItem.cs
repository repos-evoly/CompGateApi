using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class SalaryWalletBatchItem : Auditable
{
    public int Id { get; set; }

    public int SalaryWalletBatchId { get; set; }
    public SalaryWalletBatch SalaryWalletBatch { get; set; } = null!;

    public int SalaryEntryAllocationId { get; set; }
    public SalaryEntryAllocation SalaryEntryAllocation { get; set; } = null!;

    public int RequestIndex { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal Amount { get; set; }

    [MaxLength(34)]
    public string Destination { get; set; } = string.Empty;

    [MaxLength(20)]
    public string RecipientType { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Status { get; set; } = "pending";

    [Column(TypeName = "decimal(18,3)")]
    public decimal CommissionAmount { get; set; }

    [MaxLength(128)]
    public string? ProviderTransactionId { get; set; }

    [MaxLength(64)]
    public string? ProviderTxCode { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ProviderResponseRaw { get; set; }
}
