// CompGateApi.Core.Validators/ForeignTransferCreateDtoValidator.cs
using FluentValidation;
using CompGateApi.Core.Dtos;

namespace CompGateApi.Core.Validators
{
    public class ForeignTransferCreateDtoValidator : AbstractValidator<ForeignTransferCreateDto>
    {
        public ForeignTransferCreateDtoValidator()
        {
            RuleFor(x => x.ToBank)
                .NotEmpty().WithMessage("ToBank is required.")
                .MaximumLength(100);

            //         RuleFor(x => x.Branch)
            //             .NotEmpty().WithMessage("Branch is required.")
            //             .MaximumLength(100);

            //         RuleFor(x => x.ResidentSupplierName)
            //             .NotEmpty().WithMessage("ResidentSupplierName is required.")
            //             .MaximumLength(150);

            //         RuleFor(x => x.ResidentSupplierNationality)
            //             .NotEmpty().WithMessage("ResidentSupplierNationality is required.")
            //             .MaximumLength(100);

            //         RuleFor(x => x.NonResidentPassportNumber)
            //             .NotEmpty().WithMessage("NonResidentPassportNumber is required.")
            //             .MaximumLength(50);

            //         RuleFor(x => x.PlaceOfIssue)
            //             .NotEmpty().WithMessage("PlaceOfIssue is required.")
            //             .MaximumLength(100);

            //         RuleFor(x => x.DateOfIssue)
            //             .NotNull().WithMessage("DateOfIssue is required.")
            //             .LessThanOrEqualTo(DateTime.Today).WithMessage("DateOfIssue cannot be in the future.");

            //         RuleFor(x => x.NonResidentNationality)
            //             .NotEmpty().WithMessage("NonResidentNationality is required.")
            //             .MaximumLength(100);

            //         RuleFor(x => x.NonResidentAddress)
            //             .NotEmpty().WithMessage("NonResidentAddress is required.")
            //             .MaximumLength(250);

            //         RuleFor(x => x.TransferAmount)
            //             .NotNull().WithMessage("TransferAmount is required.")
            //             .GreaterThan(0).WithMessage("TransferAmount must be greater than zero.");

            //         RuleFor(x => x.ToCountry)
            //             .NotEmpty().WithMessage("ToCountry is required.")
            //             .MaximumLength(100);

            //         RuleFor(x => x.BeneficiaryName)
            //             .NotEmpty().WithMessage("BeneficiaryName is required.")
            //             .MaximumLength(150);

            //         RuleFor(x => x.BeneficiaryAddress)
            //             .NotEmpty().WithMessage("BeneficiaryAddress is required.")
            //             .MaximumLength(250);

            //         RuleFor(x => x.ExternalBankName)
            //             .NotEmpty().WithMessage("ExternalBankName is required.")
            //             .MaximumLength(150);

            //         RuleFor(x => x.ExternalBankAddress)
            //             .NotEmpty().WithMessage("ExternalBankAddress is required.")
            //             .MaximumLength(250);

            //         RuleFor(x => x.TransferToAccountNumber)
            //             .NotEmpty().WithMessage("TransferToAccountNumber is required.")
            //             .MaximumLength(50);

            //         RuleFor(x => x.TransferToAddress)
            //             .NotEmpty().WithMessage("TransferToAddress is required.")
            //             .MaximumLength(250);

            //         RuleFor(x => x.AccountHolderName)
            //             .NotEmpty().WithMessage("AccountHolderName is required.")
            //             .MaximumLength(150);

            //         RuleFor(x => x.PermanentAddress)
            //             .NotEmpty().WithMessage("PermanentAddress is required.")
            //             .MaximumLength(250);

            //         RuleFor(x => x.PurposeOfTransfer)
            //             .NotEmpty().WithMessage("PurposeOfTransfer is required.")
            //             .MaximumLength(500);
        }
    }
}


namespace CompGateApi.Core.Validators
{
    public class ForeignTransferStatusUpdateDtoValidator
        : AbstractValidator<ForeignTransferStatusUpdateDto>
    {
        public ForeignTransferStatusUpdateDtoValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status is required.")
                .MaximumLength(50);
            // Optionally .Must(s => new[] { "Pending", "Approved", "Rejected" }.Contains(s))
            //    .WithMessage("Status must be Pending, Approved or Rejected.");
        }
    }
}