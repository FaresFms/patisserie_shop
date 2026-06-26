using FluentValidation;
using Inventory.Products;

namespace Inventory.Validation;

public class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductDtoValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Category is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(128).WithMessage("Name must not exceed 128 characters.");

        RuleFor(x => x.SKU)
            .NotEmpty().WithMessage("SKU is required.")
            .MaximumLength(64).WithMessage("SKU must not exceed 64 characters.")
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("SKU may only contain letters, digits, '.', '_' or '-'.");

        RuleFor(x => x.Description)
            .MaximumLength(1024).WithMessage("Description must not exceed 1024 characters.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Unit is required.")
            .MaximumLength(32).WithMessage("Unit must not exceed 32 characters.");

        RuleFor(x => x.CostPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Cost price must be 0 or greater.");

        RuleFor(x => x.SalePrice)
            .GreaterThanOrEqualTo(0).WithMessage("Sale price must be 0 or greater.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Length(3).WithMessage("Currency must be exactly 3 characters.")
            .Matches("^[A-Z]{3}$").WithMessage("Currency must be a 3-letter ISO code (uppercase).");

        RuleFor(x => x.ReorderLevel)
            .GreaterThanOrEqualTo(0).WithMessage("Reorder level must be 0 or greater.");

        RuleFor(x => x.ImageUrl)
            .MaximumLength(512).WithMessage("Image URL must not exceed 512 characters.");

        RuleFor(x => x.ShelfLifeDays)
            .InclusiveBetween(1, 3650).When(x => x.ShelfLifeDays.HasValue)
            .WithMessage("Shelf life must be between 1 and 3650 days (leave empty for non-perishable).");

        RuleFor(x => x.ProductType)
            .NotEmpty().WithMessage("Product type is required.")
            .Must(ProductTypes.IsValid).WithMessage("Invalid product type.");
    }
}

public class UpdateProductDtoValidator : AbstractValidator<UpdateProductDto>
{
    public UpdateProductDtoValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Category is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(128).WithMessage("Name must not exceed 128 characters.");

        RuleFor(x => x.SKU)
            .NotEmpty().WithMessage("SKU is required.")
            .MaximumLength(64).WithMessage("SKU must not exceed 64 characters.")
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("SKU may only contain letters, digits, '.', '_' or '-'.");

        RuleFor(x => x.Description)
            .MaximumLength(1024).WithMessage("Description must not exceed 1024 characters.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Unit is required.")
            .MaximumLength(32).WithMessage("Unit must not exceed 32 characters.");

        RuleFor(x => x.CostPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Cost price must be 0 or greater.");

        RuleFor(x => x.SalePrice)
            .GreaterThanOrEqualTo(0).WithMessage("Sale price must be 0 or greater.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Length(3).WithMessage("Currency must be exactly 3 characters.")
            .Matches("^[A-Z]{3}$").WithMessage("Currency must be a 3-letter ISO code (uppercase).");

        RuleFor(x => x.ReorderLevel)
            .GreaterThanOrEqualTo(0).WithMessage("Reorder level must be 0 or greater.");

        RuleFor(x => x.ImageUrl)
            .MaximumLength(512).WithMessage("Image URL must not exceed 512 characters.");

        RuleFor(x => x.ShelfLifeDays)
            .InclusiveBetween(1, 3650).When(x => x.ShelfLifeDays.HasValue)
            .WithMessage("Shelf life must be between 1 and 3650 days (leave empty for non-perishable).");

        RuleFor(x => x.ProductType)
            .NotEmpty().WithMessage("Product type is required.")
            .Must(ProductTypes.IsValid).WithMessage("Invalid product type.");
    }
}
