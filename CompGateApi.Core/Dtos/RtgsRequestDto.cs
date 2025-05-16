// CompGateApi.Core.Dtos/RtgsRequestDtos.cs
using System;

namespace CompGateApi.Core.Dtos
{
    public class RtgsRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime? RefNum { get; set; }
        public DateTime? Date { get; set; }
        public string? PaymentType { get; set; }
        public string? AccountNo { get; set; }
        public string? ApplicantName { get; set; }
        public string? Address { get; set; }
        public string? BeneficiaryName { get; set; }
        public string? BeneficiaryAccountNo { get; set; }
        public string? BeneficiaryBank { get; set; }
        public string? BranchName { get; set; }
        public string? Amount { get; set; }
        public string? RemittanceInfo { get; set; }
        public bool Invoice { get; set; }
        public bool Contract { get; set; }
        public bool Claim { get; set; }
        public bool OtherDoc { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class RtgsRequestCreateDto
    {
        public DateTime? RefNum { get; set; }
        public DateTime? Date { get; set; }
        public string? PaymentType { get; set; }
        public string? AccountNo { get; set; }
        public string? ApplicantName { get; set; }
        public string? Address { get; set; }
        public string? BeneficiaryName { get; set; }
        public string? BeneficiaryAccountNo { get; set; }
        public string? BeneficiaryBank { get; set; }
        public string? BranchName { get; set; }
        public string? Amount { get; set; }
        public string? RemittanceInfo { get; set; }
        public bool Invoice { get; set; }
        public bool Contract { get; set; }
        public bool Claim { get; set; }
        public bool OtherDoc { get; set; }
    }

    public class RtgsRequestStatusUpdateDto
    {
        public string Status { get; set; } = string.Empty;
    }
}
