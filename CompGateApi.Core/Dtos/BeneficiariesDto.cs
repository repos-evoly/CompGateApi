public class BeneficiaryCreateDto
{
    public string Type { get; set; } = "local";
    public string Name { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;

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
    public string Type { get; set; } = "local";
    public string Name { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;

    public string? Address { get; set; }
    public string? Country { get; set; }
    public string? Bank { get; set; }
    public decimal? Amount { get; set; }
    public string? IntermediaryBankSwift { get; set; }
    public string? IntermediaryBankName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}


public class BeneficiaryUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Country { get; set; }
    public string? Bank { get; set; }
    public decimal? Amount { get; set; }
    public string? IntermediaryBankSwift { get; set; }
    public string? IntermediaryBankName { get; set; }
}

