using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlockingApi.Data.Models
{
    [Table("Transactions")]
    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(50)]
        public string BranchCode { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Basic { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Suffix { get; set; } = string.Empty;

        [MaxLength(50)]
        public string InputBranch { get; set; } = string.Empty;

        [MaxLength(50)]
        public string DC { get; set; } = string.Empty; // Debit or Credit

        public decimal Amount { get; set; }

        [MaxLength(3)]
        public string CCY { get; set; } = string.Empty; // Currency Code

        [MaxLength(50)]
        public string InputBranchNo { get; set; } = string.Empty;

        [MaxLength(200)]
        public string BranchName { get; set; } = string.Empty;

        public int PostingDate { get; set; }

        [MaxLength(100)]
        public string Nr1 { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Nr2 { get; set; }

        public DateTime Timestamp { get; set; }

        // Additional Fields
        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        public int InitiatorUserId { get; set; }
        public User? InitiatorUser { get; set; }  // Navigation property for Initiator (User who owns the transaction)

        public int? CurrentPartyUserId { get; set; }
        public User? CurrentPartyUser { get; set; }
        // Foreign Key to User (who approved/rejected)
        public int? ApprovedByUserId { get; set; }
        public User? ApprovedBy { get; set; } // Navigation property for approval by a user
        // New Fields
        [MaxLength(50)]
        public string TrxTagCode { get; set; } = string.Empty; // Transaction Tag Code

        [MaxLength(50)]
        public string TrxTag { get; set; } = string.Empty; // Transaction Tag

        public int TrxSeq { get; set; } // Transaction Sequence

        [MaxLength(50)]
        public string? ReconRef { get; set; } // Reconciliation Reference

        [MaxLength(50)]
        public string? EventKey { get; set; }
    }
}
