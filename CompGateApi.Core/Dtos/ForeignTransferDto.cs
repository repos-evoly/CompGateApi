using System;

namespace CompGateApi.Core.Dtos
{
    public class ForeignTransferDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? ToBank { get; set; }
        public string? Branch { get; set; }
        public string? ResidentSupplierName { get; set; }
        public string? ResidentSupplierNationality { get; set; }
        public string? NonResidentPassportNumber { get; set; }
        public string? PlaceOfIssue { get; set; }
        public DateTime? DateOfIssue { get; set; }
        public string? NonResidentNationality { get; set; }
        public string? NonResidentAddress { get; set; }
        public decimal? TransferAmount { get; set; }
        public string? ToCountry { get; set; }
        public string? BeneficiaryName { get; set; }
        public string? BeneficiaryAddress { get; set; }
        public string? ExternalBankName { get; set; }
        public string? ExternalBankAddress { get; set; }
        public string? TransferToAccountNumber { get; set; }
        public string? TransferToAddress { get; set; }
        public string? AccountHolderName { get; set; }
        public string? PermanentAddress { get; set; }
        public string? PurposeOfTransfer { get; set; }
        public string Status { get; set; } = string.Empty;

        public string? Reason { get; set; } = string.Empty; // 
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class ForeignTransferCreateDto
    {
        public string? ToBank { get; set; }
        public string? Branch { get; set; }
        public string? ResidentSupplierName { get; set; }
        public string? ResidentSupplierNationality { get; set; }
        public string? NonResidentPassportNumber { get; set; }
        public string? PlaceOfIssue { get; set; }
        public DateTime? DateOfIssue { get; set; }
        public string? NonResidentNationality { get; set; }
        public string? NonResidentAddress { get; set; }
        public decimal? TransferAmount { get; set; }
        public string? ToCountry { get; set; }
        public string? BeneficiaryName { get; set; }
        public string? BeneficiaryAddress { get; set; }
        public string? ExternalBankName { get; set; }
        public string? ExternalBankAddress { get; set; }
        public string? TransferToAccountNumber { get; set; }
        public string? TransferToAddress { get; set; }
        public string? AccountHolderName { get; set; }
        public string? PermanentAddress { get; set; }
        public string? PurposeOfTransfer { get; set; }
    }

    public class ForeignTransferStatusUpdateDto
    {
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; } // Optional reason for status change
    }
}
