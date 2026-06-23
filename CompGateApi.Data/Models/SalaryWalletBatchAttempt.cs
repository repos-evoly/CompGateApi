using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class SalaryWalletBatchAttempt : Auditable
{
    public int Id { get; set; }

    public int SalaryWalletBatchId { get; set; }
    public SalaryWalletBatch SalaryWalletBatch { get; set; } = null!;

    public int AttemptNumber { get; set; }

    [MaxLength(32)]
    public string AttemptType { get; set; } = "payment_retry";

    [MaxLength(32)]
    public string ResultStatus { get; set; } = "unknown";

    [Column(TypeName = "nvarchar(max)")]
    public string? RequestJson { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ResponseJson { get; set; }

    [MaxLength(1024)]
    public string? ErrorMessage { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
