using FluentValidation;
using Operations.Sales;

namespace Operations.Validation;

public class CreateSaleDtoValidator : AbstractValidator<CreateSaleDto>
{
    public CreateSaleDtoValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Branch is required.");

        RuleFor(x => x.SaleDate)
            .NotEmpty().WithMessage("Sale date is required.");

        RuleFor(x => x.InvoiceNumber)
            .MaximumLength(64).WithMessage("Invoice number must not exceed 64 characters.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Length(3).WithMessage("Currency must be exactly 3 characters.")
            .Matches("^[A-Z]{3}$").WithMessage("Currency must be a 3-letter ISO code (uppercase).");

        RuleFor(x => x.Notes)
            .MaximumLength(512).WithMessage("Notes must not exceed 512 characters.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Add at least one product.");

        RuleForEach(x => x.Items).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId)
                .NotEmpty().WithMessage("Product is required.");

            line.RuleFor(l => l.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than 0.");

            line.RuleFor(l => l.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Unit price must be 0 or greater.");
        });
    }
}
