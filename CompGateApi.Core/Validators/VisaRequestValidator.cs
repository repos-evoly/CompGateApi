// CompGateApi.Core.Validators/VisaRequestCreateDtoValidator.cs
using FluentValidation;
using CompGateApi.Core.Dtos;

namespace CompGateApi.Core.Validators
{
    public class VisaRequestCreateDtoValidator : AbstractValidator<VisaRequestCreateDto>
    {
        public VisaRequestCreateDtoValidator()
        {
            RuleFor(x => x.Branch)
                .NotEmpty().WithMessage("Branch is required.")
                .MaximumLength(100);

            // RuleFor(x => x.Date)
            //     .NotNull().WithMessage("Date is required.")
            //     .LessThanOrEqualTo(DateTime.Today).WithMessage("Date cannot be in the future.");

            // RuleFor(x => x.AccountHolderName)
            //     .NotEmpty().WithMessage("AccountHolderName is required.")
            //     .MaximumLength(150);

            // RuleFor(x => x.AccountNumber)
            //     .NotEmpty().WithMessage("AccountNumber is required.")
            //     .MaximumLength(50);

            // RuleFor(x => x.NationalId)
            //     .NotNull().WithMessage("NationalId is required.")
            //     .GreaterThan(0).WithMessage("NationalId must be a positive number.");

            // RuleFor(x => x.PhoneNumberLinkedToNationalId)
            //     .NotEmpty().WithMessage("PhoneNumberLinkedToNationalId is required.")
            //     .MaximumLength(20);

            // RuleFor(x => x.Cbl)
            //     .NotEmpty().WithMessage("CBL is required.")
            //     .MaximumLength(100);

            // RuleFor(x => x.CardMovementApproval)
            //     .NotEmpty().WithMessage("CardMovementApproval is required.")
            //     .MaximumLength(100);

            // RuleFor(x => x.CardUsingAcknowledgment)
            //     .NotEmpty().WithMessage("CardUsingAcknowledgment is required.")
            //     .MaximumLength(100);

            // RuleFor(x => x.ForeignAmount)
            //     .NotNull().WithMessage("ForeignAmount is required.")
            //     .GreaterThan(0).WithMessage("ForeignAmount must be greater than zero.");

            // RuleFor(x => x.LocalAmount)
            //     .NotNull().WithMessage("LocalAmount is required.")
            //     .GreaterThan(0).WithMessage("LocalAmount must be greater than zero.");

            // RuleFor(x => x.Pldedge)
            //     .NotEmpty().WithMessage("Pldedge is required.")
            //     .MaximumLength(100);
        }
    }
}

namespace CompGateApi.Core.Validators
{
    public class VisaRequestStatusUpdateDtoValidator
        : AbstractValidator<VisaRequestStatusUpdateDto>
    {
        public VisaRequestStatusUpdateDtoValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status is required.")
                .MaximumLength(50);
        }
    }
}