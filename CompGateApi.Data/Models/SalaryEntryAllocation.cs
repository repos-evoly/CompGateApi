using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CompGateApi.Data.Models;

public class SalaryEntryAllocation : Auditable
{
    public int Id { get; set; }

    public int SalaryEntryId { get; set; }
    public SalaryEntry SalaryEntry { get; set; } = null!;

    [MaxLength(20)]
    public string PaymentChannel { get; set; } = "account";

    [Column(TypeName = "decimal(18,3)")]
    public decimal Amount { get; set; }

    [MaxLength(34)]
    public string Destination { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ClientReference { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    [MaxLength(32)]
    public string? TransferResultCode { get; set; }

    [MaxLength(1024)]
    public string? TransferResultReason { get; set; }

    [MaxLength(128)]
    public string? ProviderTransactionId { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal CommissionAmount { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? RawResponse { get; set; }

    public bool IsTransferred { get; set; }
    public DateTime? TransferredAt { get; set; }
    public int? PostedByUserId { get; set; }
}
