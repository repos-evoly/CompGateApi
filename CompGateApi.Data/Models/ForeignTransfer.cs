using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("ForeignTransfers")]
    public class ForeignTransfer : Auditable
    {
        [Key]
        public int Id { get; set; }

        // Who submitted this request
        [Required]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [MaxLength(100)]
        public string? ToBank { get; set; }

        [MaxLength(100)]
        public string? Branch { get; set; }

        [MaxLength(150)]
        public string? ResidentSupplierName { get; set; }

        [MaxLength(50)]
        public string? ResidentSupplierNationality { get; set; }

        [MaxLength(50)]
        public string? NonResidentPassportNumber { get; set; }

        [MaxLength(100)]
        public string? PlaceOfIssue { get; set; }

        public DateTime? DateOfIssue { get; set; }

        [MaxLength(50)]
        public string? NonResidentNationality { get; set; }

        [MaxLength(250)]
        public string? NonResidentAddress { get; set; }

        public decimal? TransferAmount { get; set; }

        [MaxLength(100)]
        public string? ToCountry { get; set; }

        [MaxLength(150)]
        public string? BeneficiaryName { get; set; }

        [MaxLength(250)]
        public string? BeneficiaryAddress { get; set; }

        [MaxLength(150)]
        public string? ExternalBankName { get; set; }

        [MaxLength(250)]
        public string? ExternalBankAddress { get; set; }

        [MaxLength(50)]
        public string? TransferToAccountNumber { get; set; }

        [MaxLength(250)]
        public string? TransferToAddress { get; set; }

        [MaxLength(150)]
        public string? AccountHolderName { get; set; }

        [MaxLength(250)]
        public string? PermanentAddress { get; set; }

        [MaxLength(500)]
        public string? PurposeOfTransfer { get; set; }

        // Pending / Approved / Rejected
        [Required, MaxLength(50)]
        public string Status { get; set; } = "Pending";
    }
}
