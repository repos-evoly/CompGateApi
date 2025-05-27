using CompGateApi.Core.Dtos;
using FluentValidation;

namespace CompGateApi.Core.Validators
{
    public class CompanyEmployeeRegistrationDtoValidator
        : AbstractValidator<CompanyEmployeeRegistrationDto>
    {
        public CompanyEmployeeRegistrationDtoValidator()
        {
            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Username is required.")
                .MaximumLength(150);

            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("First name is required.")
                .MaximumLength(150);

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Last name is required.")
                .MaximumLength(150);

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Invalid email format.")
                .MaximumLength(150);

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(6).WithMessage("Password must be at least 6 characters.");

            RuleFor(x => x.Phone)
                .MaximumLength(15);

            RuleFor(x => x.RoleId)
                .GreaterThan(0).WithMessage("RoleId must be provided.");
        }
    }
}
