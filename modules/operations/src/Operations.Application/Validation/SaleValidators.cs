using FluentValidation;
using Microsoft.Extensions.Localization;
using Operations.Localization;
using Operations.Sales;

namespace Operations.Validation;

public class CreateSaleDtoValidator : AbstractValidator<CreateSaleDto>
{
    public CreateSaleDtoValidator(IStringLocalizer<OperationsResource> localizer)
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage(_ => localizer["Validation:BranchRequired"]);

        RuleFor(x => x.SaleDate)
            .NotEmpty().WithMessage(_ => localizer["Validation:SaleDateRequired"]);

        RuleFor(x => x.InvoiceNumber)
            .MaximumLength(64).WithMessage(_ => localizer["Validation:InvoiceNumberMax64"]);

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage(_ => localizer["Validation:CurrencyRequired"])
            .Length(3).WithMessage(_ => localizer["Validation:CurrencyLength"])
            .Matches("^[A-Z]{3}$").WithMessage(_ => localizer["Validation:CurrencyIso"]);

        RuleFor(x => x.Notes)
            .MaximumLength(512).WithMessage(_ => localizer["Validation:NotesMax512"]);

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage(_ => localizer["Validation:SaleItemsRequired"]);

        RuleForEach(x => x.Items).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId)
                .NotEmpty().WithMessage(_ => localizer["Validation:ProductRequired"]);

            line.RuleFor(l => l.Quantity)
                .GreaterThan(0).WithMessage(_ => localizer["Validation:QuantityPositive"]);

            line.RuleFor(l => l.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:UnitPriceNonNegative"]);
        });
    }
}
