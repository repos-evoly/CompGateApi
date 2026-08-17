using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    public enum OnePayTransferStatus
    {
        PendingApproval,
        PendingProviderResponse,
        Completed,
        Failed,
        Unknown
    }

    [Table("OnePayTransfers")]
    public class OnePayTransfer : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string ReferenceNo { get; set; } = string.Empty;

        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;

        public int? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }

        [Required, MaxLength(150)]
        public string CreatedByName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? ApprovedByName { get; set; }

        [Required, MaxLength(34)]
        public string FromAccount { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string FromAccountName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string FromAccountPhoneNo { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string ToInstitutionId { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string ToInstitutionName { get; set; } = string.Empty;

        [Required, MaxLength(34)]
        public string ToAccount { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string ToAccountName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,4)")]
        public decimal Amount { get; set; }

        [Required, MaxLength(10)]
        public string Currency { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required, MaxLength(2)]
        public string Language { get; set; } = "EN";

        public OnePayTransferStatus Status { get; set; } = OnePayTransferStatus.PendingApproval;

        [Required, MaxLength(200)]
        public string HostReference { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string DhbReference { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string AgreementReference { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? BankReferenceNo { get; set; }

        [MaxLength(100)]
        public string? ProviderReturnMessageCode { get; set; }

        [MaxLength(1000)]
        public string? ProviderReturnMessage { get; set; }

        [MaxLength(1000)]
        public string? ProviderGeneralError { get; set; }

        [MaxLength(1000)]
        public string? ProviderTransactionStatus { get; set; }

        public bool? ProviderMainSuccess { get; set; }
        public bool? ProviderStillUnderProcessing { get; set; }
        public bool? ProviderIsFinished { get; set; }
        public bool? ProviderIsTransactionSuccess { get; set; }

        public DateTimeOffset ValidatedAt { get; set; }
        public DateTimeOffset? ApprovedAt { get; set; }
        public DateTimeOffset? ExecutedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }

        public int StatusCheckAttempts { get; set; }
        public DateTimeOffset? LastStatusCheckedAt { get; set; }
        public DateTimeOffset? NextStatusCheckAt { get; set; }
        public DateTimeOffset? StatusCheckClaimedAt { get; set; }

        // Internal execution claim. It prevents a second checker request from
        // issuing ExecuteOutgoingTransfer while preserving the five public statuses.
        public DateTimeOffset? ExecutionClaimedAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
