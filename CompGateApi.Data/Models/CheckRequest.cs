using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("CheckRequests")]
    public class CheckRequest : Auditable
    {
        [Key]
        public int Id { get; set; }

        // Who submitted this request (local User.Id)
        [Required]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;


        [MaxLength(100)]
        public string? Branch { get; set; }

        [MaxLength(50)]
        public string? BranchNum { get; set; }

        public DateTime? Date { get; set; }

        [MaxLength(150)]
        public string? CustomerName { get; set; }

        [MaxLength(50)]
        public string? CardNum { get; set; }

        [MaxLength(50)]
        public string? AccountNum { get; set; }

        [MaxLength(150)]
        public string? Beneficiary { get; set; }

        // 1..* line items
        public ICollection<CheckRequestLineItem> LineItems { get; set; }
            = new List<CheckRequestLineItem>();

        // --------------------------------
        // Life-cycle fields:
        // --------------------------------

        // Pending / Approved / Rejected
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public string? Reason { get; set; }


        // Which admin (local User.Id) approved/rejected
        public int? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }

        // When approval happened (nullable until approved)
        public DateTimeOffset? ApprovalTimestamp { get; set; }

        // Link to your AuditLog entries if you wish
        // (optional navigational: 1..* AuditLogs related to this request)
        public ICollection<AuditLog> AuditLogs { get; set; }
            = new List<AuditLog>();
    }

    [Table("CheckRequestLineItems")]
    public class CheckRequestLineItem : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CheckRequestId { get; set; }
        public CheckRequest CheckRequest { get; set; } = null!;

        [MaxLength(50)]
        public string? Dirham { get; set; }

        [MaxLength(50)]
        public string? Lyd { get; set; }

        // You could also add quantity, amount, etc. here if needed
    }
}
