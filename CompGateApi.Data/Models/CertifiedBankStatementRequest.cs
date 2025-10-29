// Data/Models/CertifiedBankStatementRequest.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using CompGateApi.Data.Context;

namespace CompGateApi.Data.Models
{
    // these two sub‐objects will be stored inline as owned (no separate tables)
    [Owned]
    public class ServicesRequest
    {
        public bool ReactivateIdfaali { get; set; }
        public bool DeactivateIdfaali { get; set; }
        public bool ResetDigitalBankPassword { get; set; }
        public bool ResendMobileBankingPin { get; set; }
        public bool ChangePhoneNumber { get; set; }
    }

    [Owned]
    public class StatementRequest
    {
        public bool? CurrentAccountStatementArabic { get; set; }
        public bool? CurrentAccountStatementEnglish { get; set; }
        public bool? VisaAccountStatement { get; set; }
        public bool? AccountStatement { get; set; }
        public bool? JournalMovement { get; set; }
        public bool? NonFinancialCommitment { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    [Table("CertifiedBankStatementRequests")]
    public class CertifiedBankStatementRequest : Auditable
    {
        [Key] public int Id { get; set; }

        public int UserId { get; set; }
        public int CompanyId { get; set; }

        [Required, MaxLength(150)]
        public string AccountHolderName { get; set; } = null!;
        [Required, MaxLength(150)]
        public string AuthorizedOnTheAccountName { get; set; } = null!;

        [Required, MaxLength(30)]
        public string AccountNumber { get; set; } = null!;           // SOURCE account (will be debited)
        public long? OldAccountNumber { get; set; }
        public long? NewAccountNumber { get; set; }

        public ServicesRequest ServiceRequests { get; set; } = new();
        public StatementRequest StatementRequest { get; set; } = new();

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Pending";
        public string? Reason { get; set; }

        // NEW: payment audit
        public int? TransferRequestId { get; set; }
        [MaxLength(50)]
        public string? BankReference { get; set; }

        public User User { get; set; } = null!;
        public Company Company { get; set; } = null!;

        //TotalAmountLyd
        [Column(TypeName = "decimal(18,4)")]
        public decimal TotalAmountLyd { get; set; }

    }
}
