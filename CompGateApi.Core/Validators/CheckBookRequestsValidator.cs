using CompGateApi.Core.Dtos;
using FluentValidation;

namespace CompGateApi.Core.Validators
{
    public class CheckBookRequestCreateDtoValidator
        : AbstractValidator<CheckBookRequestCreateDto>
    {
        public CheckBookRequestCreateDtoValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("FullName is required.")
                .MaximumLength(150);

            RuleFor(x => x.AccountNumber)
                .NotEmpty().WithMessage("AccountNumber is required.")
                .MaximumLength(50);

            RuleFor(x => x.Branch)
                .NotEmpty().WithMessage("Branch is required.");

            RuleFor(x => x.Date)
                .NotNull().WithMessage("Date is required.")
                .LessThanOrEqualTo(DateTime.Now).WithMessage("Date cannot be in the future.");

            // other rules as desired...
        }
    }

    public class CheckBookRequestStatusUpdateDtoValidator
        : AbstractValidator<CheckBookRequestStatusUpdateDto>
    {
        public CheckBookRequestStatusUpdateDtoValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status must be provided.")
                .MaximumLength(50);
        }
    }
}