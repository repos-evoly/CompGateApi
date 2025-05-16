
using CompGateApi.Core.Dtos;
using FluentValidation;
using System;

namespace CompGateApi.Core.Validators
{
    public class ServicePackageCreateDtoValidator
        : AbstractValidator<ServicePackageCreateDto>
    {
        public ServicePackageCreateDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required");
            // … other rules …
        }
    }

    // CompGateApi.Core.Validators/ServicePackageUpdateDtoValidator.cs
    public class ServicePackageUpdateDtoValidator
        : AbstractValidator<ServicePackageUpdateDto>
    {
        public ServicePackageUpdateDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required");
            // … other rules …
        }
    }
}