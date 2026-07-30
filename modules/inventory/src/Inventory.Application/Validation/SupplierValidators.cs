using FluentValidation;
using Inventory.Localization;
using Inventory.Suppliers;
using Microsoft.Extensions.Localization;

namespace Inventory.Validation;

public class CreateSupplierDtoValidator : AbstractValidator<CreateSupplierDto>
{
    public CreateSupplierDtoValidator(IStringLocalizer<InventoryResource> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(_ => localizer["Validation:NameRequired"])
            .MaximumLength(128).WithMessage(_ => localizer["Validation:NameMax128"]);

        RuleFor(x => x.ContactPerson)
            .MaximumLength(128).WithMessage(_ => localizer["Validation:ContactPersonMax128"]);

        RuleFor(x => x.Phone)
            .MaximumLength(32).WithMessage(_ => localizer["Validation:PhoneMax32"]);

        RuleFor(x => x.Email)
            .MaximumLength(256).WithMessage(_ => localizer["Validation:EmailMax256"])
            .EmailAddress().WithMessage(_ => localizer["Validation:EmailInvalid"])
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Address)
            .MaximumLength(512).WithMessage(_ => localizer["Validation:AddressMax512"]);

        RuleFor(x => x.LeadTimeDays)
            .InclusiveBetween(0, 365).WithMessage(_ => localizer["Validation:LeadTimeRange"]);
    }
}

public class UpdateSupplierDtoValidator : AbstractValidator<UpdateSupplierDto>
{
    public UpdateSupplierDtoValidator(IStringLocalizer<InventoryResource> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(_ => localizer["Validation:NameRequired"])
            .MaximumLength(128).WithMessage(_ => localizer["Validation:NameMax128"]);

        RuleFor(x => x.ContactPerson)
            .MaximumLength(128).WithMessage(_ => localizer["Validation:ContactPersonMax128"]);

        RuleFor(x => x.Phone)
            .MaximumLength(32).WithMessage(_ => localizer["Validation:PhoneMax32"]);

        RuleFor(x => x.Email)
            .MaximumLength(256).WithMessage(_ => localizer["Validation:EmailMax256"])
            .EmailAddress().WithMessage(_ => localizer["Validation:EmailInvalid"])
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Address)
            .MaximumLength(512).WithMessage(_ => localizer["Validation:AddressMax512"]);

        RuleFor(x => x.LeadTimeDays)
            .InclusiveBetween(0, 365).WithMessage(_ => localizer["Validation:LeadTimeRange"]);
    }
}
