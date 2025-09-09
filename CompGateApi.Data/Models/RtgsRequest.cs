// CompGateApi.Data.Models/RtgsRequest.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("RtgsRequests")]
    public class RtgsRequest : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public DateTime? RefNum { get; set; }  // Could also be a string
        public DateTime? Date { get; set; }

        [MaxLength(100)]
        public string? PaymentType { get; set; }

        [MaxLength(50)]
        public string? AccountNo { get; set; }

        [MaxLength(150)]
        public string? ApplicantName { get; set; }

        [MaxLength(250)]
        public string? Address { get; set; }

        [MaxLength(150)]
        public string? BeneficiaryName { get; set; }

        [MaxLength(50)]
        public string? BeneficiaryAccountNo { get; set; }

        [MaxLength(150)]
        public string? BeneficiaryBank { get; set; }

        [MaxLength(150)]
        public string? BranchName { get; set; }

        [MaxLength(50)]
        public string? Amount { get; set; }

        [MaxLength(250)]
        public string? RemittanceInfo { get; set; }

        public bool Invoice { get; set; }
        public bool Contract { get; set; }
        public bool Claim { get; set; }
        public bool OtherDoc { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Pending";
        public string? Reason { get; set; }

        public int? TransferRequestId { get; set; }

        [MaxLength(32)]
        public string? BankReference { get; set; }


    }
}
