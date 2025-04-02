using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlockingApi.Data.Models
{
    [Table("TransactionFlows")]
    public class TransactionFlow : Auditable
    {
        [Key]
        public int Id { get; set; }

        // Foreign Key to Transaction
        public int TransactionId { get; set; }
        public Transaction Transaction { get; set; } = null!;

        // User who performed the action
        public int FromUserId { get; set; }
        public User FromUser { get; set; } = null!;

        public int? ToUserId { get; set; }
        public User? ToUser { get; set; }

        // Action that was performed (Approve, Reject, Pending, etc.)
        public string Action { get; set; } = "Pending";

        // Date and Time of the Action
        public DateTimeOffset ActionDate { get; set; } = DateTimeOffset.Now;

        // Remark for the Action
        [MaxLength(500)]
        public string Remark { get; set; } = string.Empty;

        // Boolean indicating if the transaction can be returned (reversed)
        public bool CanReturn { get; set; } = false;
    }
}
