using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("EdfaaliRequests")]
    public class EdfaaliRequest : Auditable
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
        public Representative? Representative { get; set; }

        [MaxLength(50)]
        public string? NationalId { get; set; }

        [MaxLength(50)]
        public string? IdentificationNumber { get; set; }

        [MaxLength(50)]
        public string? IdentificationType { get; set; }

        [MaxLength(200)]
        public string? CompanyEnglishName { get; set; }

        [MaxLength(250)]
        public string? WorkAddress { get; set; }

        [MaxLength(250)]
        public string? StoreAddress { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? Area { get; set; }

        [MaxLength(150)]
        public string? Street { get; set; }

        [MaxLength(50)]
        public string? MobileNumber { get; set; }

        [MaxLength(50)]
        public string? ServicePhoneNumber { get; set; }

        [MaxLength(50)]
        public string? BankAnnouncementPhoneNumber { get; set; }

        [MaxLength(200)]
        public string? Email { get; set; }

        [MaxLength(50)]
        public string? AccountNumber { get; set; }

        // Status lifecycle
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public string? Reason { get; set; }

        public int? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }
        public DateTimeOffset? ApprovalTimestamp { get; set; }

        // Attachments
        public IList<Attachment> Attachments { get; set; } = new List<Attachment>();
    }
}

