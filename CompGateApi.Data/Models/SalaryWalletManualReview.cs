using System.ComponentModel.DataAnnotations;

public class SalaryWalletManualReview : Auditable
{
    public int Id { get; set; }

    public int SalaryWalletBatchId { get; set; }
    public SalaryWalletBatch SalaryWalletBatch { get; set; } = null!;

    [MaxLength(32)]
    public string Status { get; set; } = "open";

    [MaxLength(64)]
    public string ReasonCode { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset? ResolvedAt { get; set; }

    [MaxLength(1024)]
    public string? ResolutionNote { get; set; }
}
