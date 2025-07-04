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
        RuleFor(x => x.AccountNumber).GreaterThan(0);

        // at least one section configured
        RuleFor(x => new { x.ServiceRequests, x.StatementRequest })
            .Must(x => (x.ServiceRequests != null) || (x.StatementRequest != null))
            .WithMessage("You must supply at least one of ServiceRequests or StatementRequest.");

        When(x => x.StatementRequest != null, () =>
        {
            RuleFor(x => x.StatementRequest!.FromDate)
                .LessThanOrEqualTo(x => x.StatementRequest.ToDate)
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
