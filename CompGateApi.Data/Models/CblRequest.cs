// CompGateApi.Data.Models/CblRequest.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("CblRequests")]
    public class CblRequest : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;


        [MaxLength(200)]
        public string? PartyName { get; set; }

        public decimal? Capital { get; set; }

        public DateTime? FoundingDate { get; set; }

        [MaxLength(100)]
        public string? LegalForm { get; set; }

        [MaxLength(150)]
        public string? BranchOrAgency { get; set; }

        [MaxLength(50)]
        public string? CurrentAccount { get; set; }

        public DateTime? AccountOpening { get; set; }

        [MaxLength(150)]
        public string? CommercialLicense { get; set; }

        public DateTime? ValidatyLicense { get; set; }

        [MaxLength(150)]
        public string? CommercialRegistration { get; set; }

        public DateTime? ValidatyRegister { get; set; }

        [MaxLength(150)]
        public string? StatisticalCode { get; set; }

        public DateTime? ValidatyCode { get; set; }

        [MaxLength(150)]
        public string? ChamberNumber { get; set; }

        public DateTime? ValidatyChamber { get; set; }

        [MaxLength(150)]
        public string? TaxNumber { get; set; }

        [MaxLength(150)]
        public string? Office { get; set; }

        [MaxLength(150)]
        public string? LegalRepresentative { get; set; }

        [MaxLength(50)]
        public string? RepresentativeNumber { get; set; }

        public DateTime? BirthDate { get; set; }

        [MaxLength(50)]
        public string? PassportNumber { get; set; }

        public DateTime? PassportIssuance { get; set; }

        public DateTime? PassportExpiry { get; set; }

        [MaxLength(50)]
        public string? Mobile { get; set; }

        [MaxLength(250)]
        public string? Address { get; set; }

        public DateTime? PackingDate { get; set; }

        [MaxLength(150)]
        public string? SpecialistName { get; set; }

        public string Status { get; set; } = "Pending";

        public string? Reason { get; set; }

        public ICollection<CblRequestOfficial> Officials { get; set; } = new List<CblRequestOfficial>();
        public ICollection<CblRequestSignature> Signatures { get; set; } = new List<CblRequestSignature>();
    }

    [Table("CblRequestOfficials")]
    public class CblRequestOfficial : Auditable
    {
        [Key]
        public int Id { get; set; }

        public int CblRequestId { get; set; }
        public CblRequest CblRequest { get; set; } = null!;

        [MaxLength(150)]
        public string? Name { get; set; }

        [MaxLength(150)]
        public string? Position { get; set; }
    }

    [Table("CblRequestSignatures")]
    public class CblRequestSignature : Auditable
    {
        [Key]
        public int Id { get; set; }

        public int CblRequestId { get; set; }
        public CblRequest CblRequest { get; set; } = null!;

        [MaxLength(150)]
        public string? Name { get; set; }

        [MaxLength(150)]
        public string? Signature { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Pending";
    }
}
