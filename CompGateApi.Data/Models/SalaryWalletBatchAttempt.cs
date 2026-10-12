using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class SalaryWalletBatchAttempt : Auditable
{
    public int Id { get; set; }

    public int SalaryWalletBatchId { get; set; }
    public SalaryWalletBatch SalaryWalletBatch { get; set; } = null!;

    public int AttemptNumber { get; set; }

    [MaxLength(24)]
    public string AttemptType { get; set; } = "dispatch";

    [MaxLength(32)]
    public string DispatchClassification { get; set; } = "notDispatched";

    [MaxLength(32)]
    public string ResultStatus { get; set; } = "started";

    [MaxLength(1024)]
    public string? Error { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
