using System;
using CardOpsApi.Core.Dtos;
using FluentValidation;

namespace CardOpsApi.Validators
{
    public class CurrencyCreateDtoValidator : AbstractValidator<CurrencyCreateDto>
    {
        public CurrencyCreateDtoValidator()
        {
            RuleFor(c => c.Code)
                .NotEmpty().WithMessage("Currency code is required.")
                .Length(3).WithMessage("Currency code must be exactly 3 characters.");

            RuleFor(c => c.Rate)
                .GreaterThan(0).WithMessage("Currency rate must be greater than 0.");

            RuleFor(c => c.Description)
                .NotEmpty().WithMessage("Currency description is required.");
        }
    }

    public class CurrencyUpdateDtoValidator : AbstractValidator<CurrencyUpdateDto>
    {
        public CurrencyUpdateDtoValidator()
        {
            RuleFor(c => c.Code)
                .NotEmpty().WithMessage("Currency code is required.")
                .Length(3).WithMessage("Currency code must be exactly 3 characters.");

            RuleFor(c => c.Rate)
                .GreaterThan(0).WithMessage("Currency rate must be greater than 0.");

            RuleFor(c => c.Description)
                .NotEmpty().WithMessage("Currency description is required.");
        }
    }
}
