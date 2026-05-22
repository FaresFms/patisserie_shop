using FluentValidation;
using Operations.Sales;

namespace Operations.Validation;

public class CreateSaleDtoValidator : AbstractValidator<CreateSaleDto>
{
    public CreateSaleDtoValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.SaleDate).NotEmpty();
        RuleFor(x => x.InvoiceNumber).MaximumLength(64);
        RuleFor(x => x.Currency).NotEmpty().Length(3).Matches("^[A-Z]{3}$");
        RuleFor(x => x.Notes).MaximumLength(512);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Add at least one product.");
        RuleForEach(x => x.Items).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}
