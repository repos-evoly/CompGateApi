// CompGateApi.Core.Validators/RtgsRequestValidators.cs
using CompGateApi.Core.Dtos;
using FluentValidation;
using System;

namespace CompGateApi.Core.Validators
{
    public class RtgsRequestCreateDtoValidator
        : AbstractValidator<RtgsRequestCreateDto>
    {
        public RtgsRequestCreateDtoValidator()
        {
            RuleFor(x => x.RefNum)
                .NotNull().WithMessage("RefNum is required.")
                .LessThanOrEqualTo(DateTime.Now).WithMessage("RefNum cannot be in the future.");

            RuleFor(x => x.Date)
                .NotNull().WithMessage("Date is required.")
                .LessThanOrEqualTo(DateTime.Now).WithMessage("Date cannot be in the future.");

            RuleFor(x => x.PaymentType)
                .NotEmpty().WithMessage("PaymentType is required.");

            RuleFor(x => x.AccountNo)
                .NotEmpty().WithMessage("AccountNo is required.")
                .MaximumLength(50);

            RuleFor(x => x.ApplicantName)
                .NotEmpty().WithMessage("ApplicantName is required.")
                .MaximumLength(150);

            RuleFor(x => x.Amount)
                .NotEmpty().WithMessage("Amount is required.");
        }
    }

    public class RtgsRequestStatusUpdateDtoValidator
        : AbstractValidator<RtgsRequestStatusUpdateDto>
    {
        public RtgsRequestStatusUpdateDtoValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status must be provided.")
                .MaximumLength(20);
        }
    }
}
