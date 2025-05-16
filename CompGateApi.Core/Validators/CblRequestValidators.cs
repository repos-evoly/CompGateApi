// CompGateApi.Core.Validators/CblRequestCreateDtoValidator.cs
using CompGateApi.Core.Dtos;
using FluentValidation;

namespace CompGateApi.Core.Validators
{
    public class CblRequestCreateDtoValidator : AbstractValidator<CblRequestCreateDto>
    {
        public CblRequestCreateDtoValidator()
        {
            RuleFor(x => x.PartyName)
                .NotEmpty().WithMessage("PartyName is required.")
                .MaximumLength(200);

            RuleFor(x => x.Capital)
                .NotNull().WithMessage("Capital is required.")
                .GreaterThan(0).WithMessage("Capital must be greater than zero.");

            // … add other rules for founding date, legal form, etc. …
        }
    }

    public class CblRequestStatusUpdateDtoValidator : AbstractValidator<CblRequestStatusUpdateDto>
    {
        public CblRequestStatusUpdateDtoValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status is required.")
                .MaximumLength(20).WithMessage("Status can be at most 20 characters.");
        }
    }
}
