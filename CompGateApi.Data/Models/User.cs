// CompGateApi.Data.Models/User.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Models
{
    public enum KycStatus
    {
        Missing,      // not yet looked up
        UnderReview,  // looked up & awaiting admin approval
        Approved,
        Rejected
    }

    [Table("Users")]
    [Index(nameof(Email), IsUnique = true, Name = "Unique_Email")]
    [Index(nameof(CompanyId), IsUnique = false)]
    public class User : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AuthUserId { get; set; }      // from Auth service

        /// <summary>
        /// Six-digit company code (nullable for pure bank employees)
        /// </summary>
        [MaxLength(6)]
        public string? CompanyId { get; set; }

        // ── KYC fields ────────────────────────────────────────────────

        /// <summary>Status of the KYC/registration workflow</summary>
        [Required]
        public KycStatus KycStatus { get; set; } = KycStatus.Missing;

        /// <summary>Branch ID returned by KYC lookup</summary>
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

        /// <summary>An optional message (e.g. rejection reason)</summary>
        [MaxLength(500)]
        public string? KycStatusMessage { get; set; }

        /// <summary>When the KYC lookup was requested</summary>
        public DateTime? KycRequestedAt { get; set; }

        /// <summary>When an admin approved/rejected</summary>
        public DateTime? KycReviewedAt { get; set; }


        // ── Identity fields ────────────────────────────────────────────

        [MaxLength(150)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(15)]
        public string Phone { get; set; } = string.Empty;


        // ── Role & permissions ──────────────────────────────────────

        /// <summary>1=CompanyAdmin, 2=CompanyUser, 3=BankEmployee, etc.</summary>
        [Required]
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        /// <summary>
        /// If true, this user is the “admin” of the CompanyId (can add others, manage approvals, etc.)
        /// </summary>
        public bool IsCompanyAdmin { get; set; } = false;


        // ── Service package & limits ─────────────────────────────────

        [Required]
        public int ServicePackageId { get; set; } = 1;
        public ServicePackage ServicePackage { get; set; } = null!;


        // ── Navigation collections ──────────────────────────────────

        public ICollection<AuditLog> AuditLogs { get; set; }
            = new List<AuditLog>();

        public ICollection<BankAccount> BankAccounts { get; set; }
            = new List<BankAccount>();

        public ICollection<UserRolePermission> UserRolePermissions { get; set; }
            = new List<UserRolePermission>();

        public ICollection<TransferRequest> TransferRequests { get; set; }
            = new List<TransferRequest>();

         public ICollection<VisaRequest> VisaRequests { get; set; } = new List<VisaRequest>();
         public ICollection<ForeignTransfer> ForeignTransferRequests { get; set; } = new List<ForeignTransfer>();
    }
}
