using FluentValidation;
using Inventory.Categories;
using Inventory.Localization;
using Microsoft.Extensions.Localization;

namespace Inventory.Validation;

public class CreateCategoryDtoValidator : AbstractValidator<CreateCategoryDto>
{
    public CreateCategoryDtoValidator(IStringLocalizer<InventoryResource> localizer)
    {
        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage(_ => localizer["Validation:NameRequired"])
            .MaximumLength(128).WithMessage(_ => localizer["Validation:NameMax128"]);

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(_ => localizer["Validation:NameRequired"])
            .MaximumLength(128).WithMessage(_ => localizer["Validation:NameMax128"]);

        RuleFor(x => x.DescriptionAr)
            .MaximumLength(512).WithMessage(_ => localizer["Validation:DescriptionMax512"]);

        RuleFor(x => x.DescriptionEn)
            .MaximumLength(512).WithMessage(_ => localizer["Validation:DescriptionMax512"]);
    }
}

public class UpdateCategoryDtoValidator : AbstractValidator<UpdateCategoryDto>
{
    public UpdateCategoryDtoValidator(IStringLocalizer<InventoryResource> localizer)
    {
        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage(_ => localizer["Validation:NameRequired"])
            .MaximumLength(128).WithMessage(_ => localizer["Validation:NameMax128"]);

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(_ => localizer["Validation:NameRequired"])
            .MaximumLength(128).WithMessage(_ => localizer["Validation:NameMax128"]);

        RuleFor(x => x.DescriptionAr)
            .MaximumLength(512).WithMessage(_ => localizer["Validation:DescriptionMax512"]);

        RuleFor(x => x.DescriptionEn)
            .MaximumLength(512).WithMessage(_ => localizer["Validation:DescriptionMax512"]);
    }
}
