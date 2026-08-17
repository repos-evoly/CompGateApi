using CompGateApi.Data.Models;

public class BeneficiaryCreateDto
{
    public PaymentRail PaymentRail { get; set; } = PaymentRail.Normal;
    public string Type { get; set; } = "local";
    public string Name { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? InstitutionId { get; set; }
    public string? ProviderInstitutionReference { get; set; }
    public string? InstitutionName { get; set; }
    public string Language { get; set; } = "EN";

    // Optional Fields
    public string? Address { get; set; }
    public string? Country { get; set; }
    public string? Bank { get; set; }
    public decimal? Amount { get; set; }
    public string? IntermediaryBankSwift { get; set; }
    public string? IntermediaryBankName { get; set; }
}

public class BeneficiaryDto
{
    public int Id { get; set; }
    public PaymentRail PaymentRail { get; set; } = PaymentRail.Normal;
    public string Type { get; set; } = "local";
    public string Name { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? InstitutionId { get; set; }
    public string? ProviderInstitutionReference { get; set; }
    public string? InstitutionName { get; set; }
    public int? CreatedByUserId { get; set; }

    public string? Address { get; set; }
    public string? Country { get; set; }
    public string? Bank { get; set; }
    public decimal? Amount { get; set; }
    public string? IntermediaryBankSwift { get; set; }
    public string? IntermediaryBankName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}


public class BeneficiaryUpdateDto
{
    public PaymentRail? PaymentRail { get; set; }
    public string? Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? InstitutionId { get; set; }
    public string? ProviderInstitutionReference { get; set; }
    public string? InstitutionName { get; set; }
    public string Language { get; set; } = "EN";
    public string? Address { get; set; }
    public string? Country { get; set; }
    public string? Bank { get; set; }
    public decimal? Amount { get; set; }
    public string? IntermediaryBankSwift { get; set; }
    public string? IntermediaryBankName { get; set; }
    public string? RowVersion { get; set; }
}
