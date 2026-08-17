using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    public enum PaymentRail
    {
        Normal,
        OnePay,
        LyPay
    }

    [Table("Beneficiaries")]
    public class Beneficiary : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public int? CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }

        public PaymentRail PaymentRail { get; set; } = PaymentRail.Normal;

        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = "local"; // "local" or "international"

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;

        [MaxLength(200)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        [Required]
        [MaxLength(34)]
        public string AccountNumber { get; set; } = null!;

        [MaxLength(50)]
        public string? InstitutionId { get; set; }

        [MaxLength(50)]
        public string? ProviderInstitutionReference { get; set; }

        [MaxLength(200)]
        public string? InstitutionName { get; set; }

        [MaxLength(100)]
        public string? Bank { get; set; } // For local

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Amount { get; set; } // For local

        [MaxLength(20)]
        public string? IntermediaryBankSwift { get; set; } // For international

        [MaxLength(100)]
        public string? IntermediaryBankName { get; set; } // For international

        public bool IsDeleted { get; set; } = false;

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    }
}
