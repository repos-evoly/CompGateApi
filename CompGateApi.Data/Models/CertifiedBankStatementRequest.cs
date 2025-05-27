// Data/Models/CertifiedBankStatementRequest.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using CompGateApi.Data.Context;

namespace CompGateApi.Data.Models
{
    // these two sub‐objects will be stored inline as owned (no separate tables)
    [Owned]
    public class ServicesRequest
    {
        public bool ReactivateIdfaali          { get; set; }
        public bool DeactivateIdfaali          { get; set; }
        public bool ResetDigitalBankPassword   { get; set; }
        public bool ResendMobileBankingPin     { get; set; }
        public bool ChangePhoneNumber          { get; set; }
    }

    [Owned]
    public class StatementRequest
    {
        public bool? CurrentAccountStatementArabic  { get; set; }
        public bool? CurrentAccountStatementEnglish { get; set; }
        public bool? VisaAccountStatement           { get; set; }
        public bool? AccountStatement               { get; set; }
        public bool? JournalMovement                { get; set; }
        public bool? NonFinancialCommitment         { get; set; }
        public DateTime? FromDate                   { get; set; }
        public DateTime? ToDate                     { get; set; }
    }

    [Table("CertifiedBankStatementRequests")]
    public class CertifiedBankStatementRequest : Auditable
    {
        [Key]
        public int Id { get; set; }

        // multi-tenant
        public int UserId    { get; set; }
        public int CompanyId { get; set; }

        [Required, MaxLength(150)]
        public string AccountHolderName          { get; set; } = null!;
        [Required, MaxLength(150)]
        public string AuthorizedOnTheAccountName { get; set; } = null!;
        public long AccountNumber                { get; set; }
        public long? OldAccountNumber            { get; set; }
        public long? NewAccountNumber            { get; set; }

        // owned value objects
        public ServicesRequest  ServiceRequests   { get; set; } = new();
        public StatementRequest StatementRequest  { get; set; } = new();

        // workflow
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Pending";

        public string? Reason { get; set; }

        // nav
        public User    User    { get; set; } = null!;
        public Company Company { get; set; } = null!;

    }
}
