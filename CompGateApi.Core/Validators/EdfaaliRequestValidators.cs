using CompGateApi.Core.Dtos;
using FluentValidation;

namespace CompGateApi.Core.Validators
{
    public class EdfaaliRequestCreateDtoValidator : AbstractValidator<EdfaaliRequestCreateDto>
    {
        public EdfaaliRequestCreateDtoValidator()
        {
            RuleFor(x => x.RepresentativeId)
                .GreaterThan(0).When(x => x.RepresentativeId.HasValue)
                .WithMessage("RepresentativeId must be a positive integer.");
            RuleFor(x => x.CompanyEnglishName).MaximumLength(200);
            RuleFor(x => x.WorkAddress).MaximumLength(250);
            RuleFor(x => x.StoreAddress).MaximumLength(250);
            RuleFor(x => x.City).MaximumLength(100);
            RuleFor(x => x.Area).MaximumLength(100);
            RuleFor(x => x.Street).MaximumLength(150);
            RuleFor(x => x.MobileNumber).MaximumLength(50);
            RuleFor(x => x.ServicePhoneNumber).MaximumLength(50);
            RuleFor(x => x.BankAnnouncementPhoneNumber).MaximumLength(50);
            RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
            RuleFor(x => x.AccountNumber).MaximumLength(50);
            RuleFor(x => x.IdentificationType).MaximumLength(50);
            RuleFor(x => x.IdentificationNumber).MaximumLength(50);
            RuleFor(x => x.NationalId).MaximumLength(50);
        }
    }

    public class EdfaaliRequestStatusUpdateDtoValidator : AbstractValidator<EdfaaliRequestStatusUpdateDto>
    {
        public EdfaaliRequestStatusUpdateDtoValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status is required.")
                .MaximumLength(50);
        }
    }
}
