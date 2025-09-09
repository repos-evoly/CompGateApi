using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("VisaRequests")]
    public class VisaRequest : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        // Which visa type was requested (price comes from Visas.Price)
        [Required]
        public int VisaId { get; set; }
        [ForeignKey(nameof(VisaId))]
        public Visa Visa { get; set; } = null!;

        // Optional multiplier
        public int Quantity { get; set; } = 1;

        [MaxLength(100)]
        public string? Branch { get; set; }

        public DateTime? Date { get; set; }

        [MaxLength(150)]
        public string? AccountHolderName { get; set; }

        [MaxLength(50)]
        public string? AccountNumber { get; set; }   // debited account

        public long? NationalId { get; set; }

        [MaxLength(50)]
        public string? PhoneNumberLinkedToNationalId { get; set; }

        [MaxLength(50)] public string? Cbl { get; set; }
        [MaxLength(50)] public string? CardMovementApproval { get; set; }
        [MaxLength(50)] public string? CardUsingAcknowledgment { get; set; }

        public decimal? ForeignAmount { get; set; }
        public decimal? LocalAmount { get; set; }

        [MaxLength(250)]
        public string? Pldedge { get; set; }

        [Required, MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public string? Reason { get; set; }

        // Attachments
        public Guid? AttachmentId { get; set; }
        public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();

        // ── Debit/Refund linkage like other flows ─────────────────────
        public int? TransferRequestId { get; set; }
        [ForeignKey(nameof(TransferRequestId))]
        public TransferRequest? TransferRequest { get; set; }

        [MaxLength(50)]
        public string? BankReference { get; set; }

        public int? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }
        public DateTimeOffset? ApprovalTimestamp { get; set; }

    }
}
