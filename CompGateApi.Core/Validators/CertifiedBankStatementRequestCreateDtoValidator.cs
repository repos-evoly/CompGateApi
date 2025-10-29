// CertifiedBankStatementRequestCreateDtoValidator.cs
using FluentValidation;
using CompGateApi.Core.Dtos;

public class CertifiedBankStatementRequestCreateDtoValidator
    : AbstractValidator<CertifiedBankStatementRequestCreateDto>
{
    public CertifiedBankStatementRequestCreateDtoValidator()
    {
        RuleFor(x => x.AccountHolderName).NotEmpty();
        RuleFor(x => x.AuthorizedOnTheAccountName).NotEmpty();
        RuleFor(x => x.AccountNumber)
            .NotEmpty()
            .MaximumLength(30)
            .Matches("^\\d{1,30}$").WithMessage("AccountNumber must be digits only (max 30).");

        // at least one section configured
        RuleFor(x => new { x.ServiceRequests, x.StatementRequest })
            .Must(x => (x.ServiceRequests != null) || (x.StatementRequest != null))
            .WithMessage("You must supply at least one of ServiceRequests or StatementRequest.");

        When(x => x.StatementRequest != null, () =>
        {
            RuleFor(x => x.StatementRequest!.FromDate)
                .LessThanOrEqualTo(x => x.StatementRequest != null ? x.StatementRequest.ToDate : null)
                .When(x => x.StatementRequest!.FromDate.HasValue && x.StatementRequest.ToDate.HasValue);
        });
    }
}


public class CertifiedBankStatementRequestStatusUpdateDtoValidator
    : AbstractValidator<CertifiedBankStatementRequestStatusUpdateDto>
{
    public CertifiedBankStatementRequestStatusUpdateDtoValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty();
            // .Must(s => new[] { "Pending", "Approved", "Declined" }.Contains(s))
            // .WithMessage("Status must be Pending, Approved or Declined");
        RuleFor(x => x.Reason)
            .NotEmpty()
            .When(x => x.Status != "Pending");
    }
}
