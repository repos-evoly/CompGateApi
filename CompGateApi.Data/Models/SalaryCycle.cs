using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CompGateApi.Data.Models;
public class SalaryCycle : Auditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    [MaxLength(20)]
    public string SalaryMonth { get; set; } = string.Empty;

    // Stores numeric month as string (per requirements)
    [MaxLength(10)]
    public string? AdditionalMonth { get; set; }
    public int CreatedByUserId { get; set; }

    /* NEW ↓ */
    [MaxLength(34)] public string DebitAccount { get; set; } = string.Empty;
    [MaxLength(3)] public string Currency { get; set; } = "LYD";

    public int? PostedByUserId { get; set; }
    public DateTime? PostedAt { get; set; }

    [MaxLength(32)]
    public string PostingStatus { get; set; } = "notPosted";

    public DateTimeOffset? PostingStartedAt { get; set; }
    public DateTimeOffset? PostingCompletedAt { get; set; }
    public DateTimeOffset? PostingClaimedAt { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal TotalAmount { get; set; }

    [MaxLength(32)]
    public string? BankReference { get; set; }       // HID/referenceId sent to bank

    [Column(TypeName = "nvarchar(max)")]
    public string? BankResponseRaw { get; set; }

    [MaxLength(32)] public string? BankFeeReference { get; set; }
    public string? BankFeeResponseRaw { get; set; }

    [MaxLength(32)]
    public string BankFeeStatus { get; set; } = "notRequired";

    [MaxLength(1024)]
    public string? BankFeeError { get; set; }

    // Accumulates repost attempts with salary + fee references
    // JSON array of objects: [{"attemptAtUtc": "...", "entryIds": [..], "salaryRef": "...", "feeRef": "..."}, ...]
    [Column(TypeName = "nvarchar(max)")]
    public string? BankBatchHistoryJson { get; set; }

    public ICollection<SalaryEntry> Entries { get; set; } = new List<SalaryEntry>();
    public ICollection<SalaryWalletBatch> WalletBatches { get; set; } = new List<SalaryWalletBatch>();

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
