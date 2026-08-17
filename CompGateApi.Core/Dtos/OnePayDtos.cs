using CompGateApi.Data.Models;

namespace CompGateApi.Core.Dtos;

public sealed class OnePayAccountDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal AvailableBalance { get; set; }
}

public sealed class OnePayInstitutionDto
{
    public string InstitutionId { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public sealed class OnePayClientDeviceInfoDto
{
    public string? DeviceLat { get; set; }
    public string? DeviceLon { get; set; }
}

public sealed class OnePayValidateTransferRequestDto
{
    public int? BeneficiaryId { get; set; }
    public string FromAccount { get; set; } = string.Empty;
    public string ToInstitutionId { get; set; } = string.Empty;
    public string ToAccount { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Language { get; set; } = "EN";
    public OnePayClientDeviceInfoDto? DeviceInfo { get; set; }
}

public sealed class OnePayCreateTransferRequestDto
{
    public Guid ValidationToken { get; set; }
}

public sealed class OnePayValidationDto
{
    public Guid ValidationToken { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string ToAccountName { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;
    public string FromAccount { get; set; } = string.Empty;
    public string ToAccount { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class OnePayTransferDto
{
    public int Id { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public string FromAccount { get; set; } = string.Empty;
    public string FromAccountName { get; set; } = string.Empty;
    public string ToInstitutionId { get; set; } = string.Empty;
    public string ToInstitutionName { get; set; } = string.Empty;
    public string ToAccount { get; set; } = string.Empty;
    public string ToAccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public OnePayTransferStatus Status { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? ApprovedByName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ValidatedAt { get; set; }
    public DateTimeOffset? ExecutedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int StatusCheckAttempts { get; set; }
    public DateTimeOffset? LastStatusCheckedAt { get; set; }
    public string? ProviderTransactionStatus { get; set; }
    public string? ProviderReturnMessageCode { get; set; }
    public string? ProviderReturnMessage { get; set; }
    public string? ProviderGeneralError { get; set; }
    public bool? ProviderMainSuccess { get; set; }
    public bool? ProviderStillUnderProcessing { get; set; }
    public bool? ProviderIsFinished { get; set; }
    public bool? ProviderIsTransactionSuccess { get; set; }
    public string? BankReferenceNo { get; set; }
    public bool CanApprove { get; set; }
}

public sealed class OnePayCurrentUser
{
    public int UserId { get; set; }
    public int CompanyId { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyPhone { get; set; } = string.Empty;
    public string UserPhone { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IReadOnlySet<string> Permissions { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool HasPermission(string permission) => Permissions.Contains(permission);
}
