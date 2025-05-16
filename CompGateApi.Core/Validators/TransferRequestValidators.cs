// CompGateApi.Core.Validators/TransferRequestCreateDtoValidator.cs
using FluentValidation;
using CompGateApi.Core.Dtos;

namespace CompGateApi.Core.Validators
{
    public class TransferRequestCreateDtoValidator
        : AbstractValidator<TransferRequestCreateDto>
    {
        public TransferRequestCreateDtoValidator()
        {
            RuleFor(x => x.TransactionCategoryId).NotEmpty();
            RuleFor(x => x.FromAccount).NotEmpty().Length(1, 34);
            RuleFor(x => x.ToAccount).NotEmpty().Length(1, 34);
            RuleFor(x => x.Amount).GreaterThan(0);
            RuleFor(x => x.CurrencyId).NotEmpty();
        }
    }

    public class TransferRequestStatusUpdateDtoValidator
        : AbstractValidator<TransferRequestStatusUpdateDto>
    {
        public TransferRequestStatusUpdateDtoValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty()
                .Must(s => new[] { "Pending", "Completed", "Failed" }
                    .Contains(s))
                .WithMessage("Status must be Pending, Completed or Failed.");
        }
    }
}
