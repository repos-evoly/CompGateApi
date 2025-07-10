// CompGateApi.Core.Dtos/CblRequestDtos.cs
using System;
using System.Collections.Generic;

namespace CompGateApi.Core.Dtos
{
    public class CblRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? PartyName { get; set; }
        public decimal? Capital { get; set; }
        public DateTime? FoundingDate { get; set; }
        public string? LegalForm { get; set; }
        public string? BranchOrAgency { get; set; }
        public string? CurrentAccount { get; set; }
        public DateTime? AccountOpening { get; set; }
        public string? CommercialLicense { get; set; }
        public DateTime? ValidatyLicense { get; set; }
        public string? CommercialRegistration { get; set; }
        public DateTime? ValidatyRegister { get; set; }
        public string? StatisticalCode { get; set; }
        public DateTime? ValidatyCode { get; set; }
        public string? ChamberNumber { get; set; }
        public DateTime? ValidatyChamber { get; set; }
        public string? TaxNumber { get; set; }
        public string? Office { get; set; }
        public string? LegalRepresentative { get; set; }
        public string? RepresentativeNumber { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? PassportNumber { get; set; }
        public DateTime? PassportIssuance { get; set; }
        public DateTime? PassportExpiry { get; set; }
        public string? Mobile { get; set; }
        public string? Address { get; set; }
        public DateTime? PackingDate { get; set; }
        public string? SpecialistName { get; set; }
        public string Status { get; set; } = string.Empty;

        public Guid? AttachmentId { get; set; }
        public AttachmentDto? Attachment { get; set; }

        public List<CblRequestOfficialDto> Officials { get; set; } = new();
        public List<CblRequestSignatureDto> Signatures { get; set; } = new();

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class CblRequestOfficialDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Position { get; set; }
    }

    public class CblRequestSignatureDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Signature { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class CblRequestCreateDto
    {
        public string? PartyName { get; set; }
        public decimal? Capital { get; set; }
        public DateTime? FoundingDate { get; set; }
        public string? LegalForm { get; set; }
        public string? BranchOrAgency { get; set; }
        public string? CurrentAccount { get; set; }
        public DateTime? AccountOpening { get; set; }
        public string? CommercialLicense { get; set; }
        public DateTime? ValidatyLicense { get; set; }
        public string? CommercialRegistration { get; set; }
        public DateTime? ValidatyRegister { get; set; }
        public string? StatisticalCode { get; set; }
        public DateTime? ValidatyCode { get; set; }
        public string? ChamberNumber { get; set; }
        public DateTime? ValidatyChamber { get; set; }
        public string? TaxNumber { get; set; }
        public string? Office { get; set; }
        public string? LegalRepresentative { get; set; }
        public string? RepresentativeNumber { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? PassportNumber { get; set; }
        public DateTime? PassportIssuance { get; set; }
        public DateTime? PassportExpiry { get; set; }
        public string? Mobile { get; set; }
        public string? Address { get; set; }
        public DateTime? PackingDate { get; set; }
        public string? SpecialistName { get; set; }

        public List<CblRequestOfficialDto> Officials { get; set; } = new();
        public List<CblRequestSignatureDto> Signatures { get; set; } = new();
    }

    public class CblRequestStatusUpdateDto
    {
        public string Status { get; set; } = string.Empty;


    }


}
