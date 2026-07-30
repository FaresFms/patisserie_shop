using FluentValidation;
using Inventory.Localization;
using Inventory.Products;
using Microsoft.Extensions.Localization;

namespace Inventory.Validation;

public class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductDtoValidator(IStringLocalizer<InventoryResource> localizer)
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage(_ => localizer["Validation:CategoryRequired"]);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(_ => localizer["Validation:NameRequired"])
            .MaximumLength(128).WithMessage(_ => localizer["Validation:NameMax128"]);

        RuleFor(x => x.SKU)
            .NotEmpty().WithMessage(_ => localizer["Validation:SkuRequired"])
            .MaximumLength(64).WithMessage(_ => localizer["Validation:SkuMax64"])
            .Matches("^[A-Za-z0-9._-]+$").WithMessage(_ => localizer["Validation:SkuFormat"]);

        RuleFor(x => x.Description)
            .MaximumLength(1024).WithMessage(_ => localizer["Validation:DescriptionMax1024"]);

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage(_ => localizer["Validation:UnitRequired"])
            .MaximumLength(32).WithMessage(_ => localizer["Validation:UnitMax32"]);

        RuleFor(x => x.CostPrice)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:CostPriceNonNegative"]);

        RuleFor(x => x.SalePrice)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:SalePriceNonNegative"]);

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage(_ => localizer["Validation:CurrencyRequired"])
            .Length(3).WithMessage(_ => localizer["Validation:CurrencyLength"])
            .Matches("^[A-Z]{3}$").WithMessage(_ => localizer["Validation:CurrencyIso"]);

        RuleFor(x => x.ReorderLevel)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:ReorderLevelNonNegative"]);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(512).WithMessage(_ => localizer["Validation:ImageUrlMax512"]);

        RuleFor(x => x.ShelfLifeDays)
            .InclusiveBetween(1, 3650).When(x => x.ShelfLifeDays.HasValue)
            .WithMessage(_ => localizer["Validation:ShelfLifeRange"]);

        RuleFor(x => x.ProductType)
            .NotEmpty().WithMessage(_ => localizer["Validation:ProductTypeRequired"])
            .Must(ProductTypes.IsValid).WithMessage(_ => localizer["Validation:ProductTypeInvalid"]);
    }
}

public class UpdateProductDtoValidator : AbstractValidator<UpdateProductDto>
{
    public UpdateProductDtoValidator(IStringLocalizer<InventoryResource> localizer)
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage(_ => localizer["Validation:CategoryRequired"]);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(_ => localizer["Validation:NameRequired"])
            .MaximumLength(128).WithMessage(_ => localizer["Validation:NameMax128"]);

        RuleFor(x => x.SKU)
            .NotEmpty().WithMessage(_ => localizer["Validation:SkuRequired"])
            .MaximumLength(64).WithMessage(_ => localizer["Validation:SkuMax64"])
            .Matches("^[A-Za-z0-9._-]+$").WithMessage(_ => localizer["Validation:SkuFormat"]);

        RuleFor(x => x.Description)
            .MaximumLength(1024).WithMessage(_ => localizer["Validation:DescriptionMax1024"]);

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage(_ => localizer["Validation:UnitRequired"])
            .MaximumLength(32).WithMessage(_ => localizer["Validation:UnitMax32"]);

        RuleFor(x => x.CostPrice)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:CostPriceNonNegative"]);

        RuleFor(x => x.SalePrice)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:SalePriceNonNegative"]);

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage(_ => localizer["Validation:CurrencyRequired"])
            .Length(3).WithMessage(_ => localizer["Validation:CurrencyLength"])
            .Matches("^[A-Z]{3}$").WithMessage(_ => localizer["Validation:CurrencyIso"]);

        RuleFor(x => x.ReorderLevel)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:ReorderLevelNonNegative"]);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(512).WithMessage(_ => localizer["Validation:ImageUrlMax512"]);

        RuleFor(x => x.ShelfLifeDays)
            .InclusiveBetween(1, 3650).When(x => x.ShelfLifeDays.HasValue)
            .WithMessage(_ => localizer["Validation:ShelfLifeRange"]);

        RuleFor(x => x.ProductType)
            .NotEmpty().WithMessage(_ => localizer["Validation:ProductTypeRequired"])
            .Must(ProductTypes.IsValid).WithMessage(_ => localizer["Validation:ProductTypeInvalid"]);
    }
}
