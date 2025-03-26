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

        public string Initiator { get; set; } = string.Empty; // User who owns the transaction
        public string CurrentParty { get; set; } = string.Empty; // Party currently handling the transaction
        // Foreign Key to User (who approved/rejected)
        public int? ApprovedByUserId { get; set; }
        public User? ApprovedBy { get; set; } // Navigation property for approval by a user
    }
}
