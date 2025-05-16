// CompGateApi.Core.Validators/CheckRequestValidators.cs
using System;
using CompGateApi.Core.Dtos;
using FluentValidation;

namespace CompGateApi.Core.Validators
{
    public class CheckRequestCreateDtoValidator : AbstractValidator<CheckRequestCreateDto>
    {
        public CheckRequestCreateDtoValidator()
        {
            RuleFor(x => x.Branch)
                .NotEmpty().WithMessage("Branch is required.")
                .MaximumLength(100);

            RuleFor(x => x.AccountNum)
                .NotEmpty().WithMessage("Account number is required.")
                .MaximumLength(50);

            RuleFor(x => x.Date)
                .NotNull().WithMessage("Date is required.")
                .LessThanOrEqualTo(DateTime.Now).WithMessage("Date cannot be in the future.");

            RuleForEach(x => x.LineItems)
                .ChildRules(items =>
                {
                    items.RuleFor(li => li.Dirham)
                         .NotEmpty().WithMessage("Dirham amount required.");
                    items.RuleFor(li => li.Lyd)
                         .NotEmpty().WithMessage("Lyd amount required.");
                });
        }
    }

    public class CheckRequestStatusUpdateDtoValidator : AbstractValidator<CheckRequestStatusUpdateDto>
    {
        public CheckRequestStatusUpdateDtoValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status must be provided.")
                .MaximumLength(50);
        }
    }
}
