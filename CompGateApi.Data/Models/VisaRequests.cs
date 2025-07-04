// ── CompGateApi.Data.Models/VisaRequest.cs ─────────────────────────────
using System;
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

        [MaxLength(100)]
        public string? Branch { get; set; }

        public DateTime? Date { get; set; }

        [MaxLength(150)]
        public string? AccountHolderName { get; set; }

        [MaxLength(50)]
        public string? AccountNumber { get; set; }

        public long? NationalId { get; set; }

        [MaxLength(50)]
        public string? PhoneNumberLinkedToNationalId { get; set; }

        [MaxLength(50)]
        public string? Cbl { get; set; }

        [MaxLength(50)]
        public string? CardMovementApproval { get; set; }

        [MaxLength(50)]
        public string? CardUsingAcknowledgment { get; set; }

        public decimal? ForeignAmount { get; set; }
        public decimal? LocalAmount { get; set; }

        [MaxLength(250)]
        public string? Pldedge { get; set; }

        [Required, MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public string? Reason { get; set; }

    }
}
