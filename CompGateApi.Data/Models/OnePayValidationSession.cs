using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("OnePayValidationSessions")]
    public class OnePayValidationSession : Auditable
    {
        [Key]
        public Guid Id { get; set; }

        [Required, MaxLength(50)]
        public string ReferenceNo { get; set; } = string.Empty;

        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;

        [Required, MaxLength(150)]
        public string CreatedByName { get; set; } = string.Empty;

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

        [Required, MaxLength(200)]
        public string HostReference { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string DhbReference { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string AgreementReference { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? BankReferenceNo { get; set; }

        public DateTimeOffset ValidatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? ConsumedAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
