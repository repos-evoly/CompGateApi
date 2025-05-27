// CreditFacilitiesOrLetterOfGuaranteeRequestCreateDtoValidator.cs
using System;
using FluentValidation;
using CompGateApi.Core.Dtos;

public class CreditFacilitiesOrLetterOfGuaranteeRequestCreateDtoValidator
    : AbstractValidator<CreditFacilitiesOrLetterOfGuaranteeRequestCreateDto>
{
    public CreditFacilitiesOrLetterOfGuaranteeRequestCreateDtoValidator()
    {
        RuleFor(x => x.AccountNumber)
            .NotEmpty();
        RuleFor(x => x.Date)
            .LessThanOrEqualTo(DateTime.Today);
        RuleFor(x => x.Amount)
            .GreaterThan(0);
        RuleFor(x => x.Purpose)
            .NotEmpty();
        RuleFor(x => x.Curr)
            .NotEmpty();
        RuleFor(x => x.ReferenceNumber)
            .NotEmpty();
        RuleFor(x => x.Type)
            .NotEmpty();
    }
}



public class CreditFacilitiesOrLetterOfGuaranteeRequestStatusUpdateDtoValidator
    : AbstractValidator<CreditFacilitiesOrLetterOfGuaranteeRequestStatusUpdateDto>
{
    public CreditFacilitiesOrLetterOfGuaranteeRequestStatusUpdateDtoValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => new[] { "Pending", "Approved", "Declined" }.Contains(s))
            .WithMessage("Status must be Pending, Approved or Declined");
        RuleFor(x => x.Reason)
            .NotEmpty()
            .When(x => x.Status != "Pending");
    }
}
