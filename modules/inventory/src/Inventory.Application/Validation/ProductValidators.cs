using FluentValidation;
using Inventory.Products;

namespace Inventory.Validation;

public class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductDtoValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.SKU).NotEmpty().MaximumLength(64)
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("SKU may only contain letters, digits, '.', '_' or '-'.");
        RuleFor(x => x.Description).MaximumLength(1024);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(32);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SalePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3)
            .Matches("^[A-Z]{3}$").WithMessage("Currency must be a 3-letter ISO code (uppercase).");
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ImageUrl).MaximumLength(512);
    }
}

public class UpdateProductDtoValidator : AbstractValidator<UpdateProductDto>
{
    public UpdateProductDtoValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.SKU).NotEmpty().MaximumLength(64)
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("SKU may only contain letters, digits, '.', '_' or '-'.");
        RuleFor(x => x.Description).MaximumLength(1024);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(32);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SalePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3)
            .Matches("^[A-Z]{3}$").WithMessage("Currency must be a 3-letter ISO code (uppercase).");
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ImageUrl).MaximumLength(512);
    }
}
