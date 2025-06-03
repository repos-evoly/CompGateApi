// CompGateApi.Data.Models/Company.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("Companies")]
    public class Company
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(6)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public RegistrationStatus RegistrationStatus { get; set; } = RegistrationStatus.UnderReview;
        public string? RegistrationStatusMessage { get; set; }
        public DateTimeOffset KycRequestedAt { get; set; } = DateTimeOffset.Now;
        public DateTimeOffset? KycReviewedAt { get; set; }

        [MaxLength(10)]
        public string? KycBranchId { get; set; }

        [MaxLength(150)]
        public string? KycLegalCompanyName { get; set; }

        [MaxLength(150)]
        public string? KycLegalCompanyNameLt { get; set; }

        [MaxLength(50)]
        public string? KycMobile { get; set; }

        [MaxLength(100)]
        public string? KycNationality { get; set; }

        [MaxLength(100)]
        public string? KycCity { get; set; }

        // ── Users in this company ─────────────────────────────────────────────
        public ICollection<User> Users { get; set; } = new List<User>();

        public int ServicePackageId { get; set; }

        public ServicePackage ServicePackage { get; set; } = null!;

        // ── Requests submitted by any user in the company ────────────────────
        public ICollection<TransferRequest> TransferRequests { get; set; }
            = new List<TransferRequest>();

        public ICollection<VisaRequest> VisaRequests { get; set; }
            = new List<VisaRequest>();

        public ICollection<ForeignTransfer> ForeignTransfers { get; set; }
            = new List<ForeignTransfer>();

        public ICollection<CheckRequest> CheckRequests { get; set; }
            = new List<CheckRequest>();

        public ICollection<CheckBookRequest> CheckBookRequests { get; set; }
            = new List<CheckBookRequest>();

        public ICollection<CblRequest> CblRequests { get; set; }
            = new List<CblRequest>();

        public ICollection<RtgsRequest> RtgsRequests { get; set; }
            = new List<RtgsRequest>();

        public ICollection<CreditFacilitiesOrLetterOfGuaranteeRequest> CreditFacilitiesRequests
        { get; set; } = new List<CreditFacilitiesOrLetterOfGuaranteeRequest>();

        // ── **New** Certified bank-statement requests
        public ICollection<CertifiedBankStatementRequest> CertifiedBankStatementRequests
        { get; set; } = new List<CertifiedBankStatementRequest>();

        public ICollection<Attachment> Attachments { get; set; }
       = new List<Attachment>();
    }

    public enum RegistrationStatus
    {
        MissingsDocuments,
        MissingKyc,
        MissingInformation,
        UnderReview,
        Approved,
        Active,
        Rejected,
        Error
    }

}
