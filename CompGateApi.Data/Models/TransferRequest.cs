// CompGateApi.Data.Models/TransferRequest.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    /// <summary>
    /// When a user initiates a transfer of any TransactionCategory:
    /// tracks amount, currency, status, timestamps, etc.
    /// </summary>
    [Table("TransferRequests")]
    public class TransferRequest : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        [Required]
        public int TransactionCategoryId { get; set; }
        public TransactionCategory TransactionCategory { get; set; } = null!;

        [Required, MaxLength(34)]
        public string FromAccount { get; set; } = string.Empty;

        [Required, MaxLength(34)]
        public string ToAccount { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal Amount { get; set; }

        [Required]
        public int CurrencyId { get; set; }
        public Currency Currency { get; set; } = null!;

        [Required]
        public int ServicePackageId { get; set; }
        public ServicePackage ServicePackage { get; set; } = null!;

        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public decimal CommissionAmount { get; set; }
        public bool CommissionOnRecipient { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal Rate { get; set; }

        public int? EconomicSectorId { get; set; }
        public EconomicSector? EconomicSector { get; set; } = null!;


        [MaxLength(3)]
        public string TransferMode { get; set; } = "B2B";

        public string? GroupId { get; set; }

    }
}
