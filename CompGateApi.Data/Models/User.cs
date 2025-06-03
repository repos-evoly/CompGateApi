using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Models
{
   
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
        /// Foreign key to Company
        /// </summary>

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

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
        /// If true, this user is the “admin” of the Company
        /// </summary>
        public bool IsCompanyAdmin { get; set; } = false;


        // ── Service package & limits ─────────────────────────────────

        public int? ServicePackageId { get; set; }
        public ServicePackage? ServicePackage { get; set; }

        // ── Navigation collections ──────────────────────────────────

        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
        public ICollection<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();
        public ICollection<UserRolePermission> UserRolePermissions { get; set; } = new List<UserRolePermission>();

        // Requests submitted by this user
        public ICollection<TransferRequest> TransferRequests { get; set; } = new List<TransferRequest>();
        public ICollection<VisaRequest> VisaRequests { get; set; } = new List<VisaRequest>();
        public ICollection<ForeignTransfer> ForeignTransferRequests { get; set; } = new List<ForeignTransfer>();
        public ICollection<CheckRequest> CheckRequests { get; set; } = new List<CheckRequest>();
        public ICollection<CheckBookRequest> CheckBookRequests { get; set; } = new List<CheckBookRequest>();
        public ICollection<CblRequest> CblRequests { get; set; } = new List<CblRequest>();
        public ICollection<RtgsRequest> RtgsRequests { get; set; } = new List<RtgsRequest>();

        public ICollection<CreditFacilitiesOrLetterOfGuaranteeRequest> CreditFacilitiesRequests
            = new List<CreditFacilitiesOrLetterOfGuaranteeRequest>();

        public ICollection<CertifiedBankStatementRequest> CertifiedBankStatementRequests
            = new List<CertifiedBankStatementRequest>();

    }
}