using FluentValidation;
using Inventory.Branches;
using Inventory.Localization;
using Microsoft.Extensions.Localization;

namespace Inventory.Validation;

public class CreateBranchDtoValidator : AbstractValidator<CreateBranchDto>
{
    public CreateBranchDtoValidator(IStringLocalizer<InventoryResource> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(_ => localizer["Validation:NameRequired"])
            .MaximumLength(128).WithMessage(_ => localizer["Validation:NameMax128"]);

        RuleFor(x => x.Address)
            .MaximumLength(512).WithMessage(_ => localizer["Validation:AddressMax512"]);

        RuleFor(x => x.Phone)
            .MaximumLength(32).WithMessage(_ => localizer["Validation:PhoneMax32"]);

        RuleFor(x => x.Email)
            .MaximumLength(256).WithMessage(_ => localizer["Validation:EmailMax256"])
            .EmailAddress().WithMessage(_ => localizer["Validation:EmailInvalid"])
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.BranchType)
            .NotEmpty().WithMessage(_ => localizer["Validation:BranchTypeRequired"])
            .Must(BranchTypes.IsValid).WithMessage(_ => localizer["Validation:BranchTypeInvalid"]);
    }
}

public class UpdateBranchDtoValidator : AbstractValidator<UpdateBranchDto>
{
    public UpdateBranchDtoValidator(IStringLocalizer<InventoryResource> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(_ => localizer["Validation:NameRequired"])
            .MaximumLength(128).WithMessage(_ => localizer["Validation:NameMax128"]);

        RuleFor(x => x.Address)
            .MaximumLength(512).WithMessage(_ => localizer["Validation:AddressMax512"]);

        RuleFor(x => x.Phone)
            .MaximumLength(32).WithMessage(_ => localizer["Validation:PhoneMax32"]);

        RuleFor(x => x.Email)
            .MaximumLength(256).WithMessage(_ => localizer["Validation:EmailMax256"])
            .EmailAddress().WithMessage(_ => localizer["Validation:EmailInvalid"])
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.BranchType)
            .NotEmpty().WithMessage(_ => localizer["Validation:BranchTypeRequired"])
            .Must(BranchTypes.IsValid).WithMessage(_ => localizer["Validation:BranchTypeInvalid"]);
    }
}
