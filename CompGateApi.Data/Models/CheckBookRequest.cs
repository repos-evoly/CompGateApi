using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("CheckBookRequests")]
    public class CheckBookRequest : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public int? RepresentativeId { get; set; }

        [ForeignKey(nameof(RepresentativeId))]
        public Representative? Representative { get; set; } = null!;

        [MaxLength(150)]
        public string? FullName { get; set; }

        [MaxLength(250)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? AccountNumber { get; set; }

        [MaxLength(250)]
        public string? PleaseSend { get; set; }

        [MaxLength(100)]
        public string? Branch { get; set; }

        public DateTime? Date { get; set; }

        /// <summary>Expected values: "24" or "48"</summary>
        [MaxLength(50)]
        public string? BookContaining { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        public string? Reason { get; set; }

        /// <summary>Local TransferRequest Id created for the debit.</summary>
        public int? TransferRequestId { get; set; }

        /// <summary>Original bank referenceId used for the debit (needed for reversal).</summary>
        [MaxLength(32)]
        public string? BankReference { get; set; }
    }
}
