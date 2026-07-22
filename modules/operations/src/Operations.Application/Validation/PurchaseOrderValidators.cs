using System.Linq;
using FluentValidation;
using Microsoft.Extensions.Localization;
using Operations.Localization;
using Operations.PurchaseOrders;

namespace Operations.Validation;

public class CreatePurchaseOrderDtoValidator : AbstractValidator<CreatePurchaseOrderDto>
{
    public CreatePurchaseOrderDtoValidator(IStringLocalizer<OperationsResource> localizer)
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage(_ => localizer["Validation:SupplierRequired"]);

        RuleFor(x => x.DestBranchId)
            .NotEmpty().WithMessage(_ => localizer["Validation:DestinationBranchRequired"]);

        RuleFor(x => x.OrderDate)
            .NotEmpty().WithMessage(_ => localizer["Validation:OrderDateRequired"]);

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage(_ => localizer["Validation:CurrencyRequired"])
            .Length(3).WithMessage(_ => localizer["Validation:CurrencyLength"])
            .Matches("^[A-Z]{3}$").WithMessage(_ => localizer["Validation:CurrencyIso"]);

        RuleFor(x => x.Notes)
            .MaximumLength(512).WithMessage(_ => localizer["Validation:NotesMax512"]);

        RuleFor(x => x)
            .Must(x => !x.ExpectedDeliveryDate.HasValue || x.ExpectedDeliveryDate.Value >= x.OrderDate)
            .WithMessage(_ => localizer["Validation:ExpectedDeliveryBeforeOrder"]);
    }
}

public class UpdatePurchaseOrderHeaderDtoValidator : AbstractValidator<UpdatePurchaseOrderHeaderDto>
{
    public UpdatePurchaseOrderHeaderDtoValidator(IStringLocalizer<OperationsResource> localizer)
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage(_ => localizer["Validation:SupplierRequired"]);

        RuleFor(x => x.DestBranchId)
            .NotEmpty().WithMessage(_ => localizer["Validation:DestinationBranchRequired"]);

        RuleFor(x => x.OrderDate)
            .NotEmpty().WithMessage(_ => localizer["Validation:OrderDateRequired"]);

        RuleFor(x => x.Notes)
            .MaximumLength(512).WithMessage(_ => localizer["Validation:NotesMax512"]);

        RuleFor(x => x)
            .Must(x => !x.ExpectedDeliveryDate.HasValue || x.ExpectedDeliveryDate.Value >= x.OrderDate)
            .WithMessage(_ => localizer["Validation:ExpectedDeliveryBeforeOrder"]);
    }
}

public class AddPurchaseOrderItemDtoValidator : AbstractValidator<AddPurchaseOrderItemDto>
{
    public AddPurchaseOrderItemDtoValidator(IStringLocalizer<OperationsResource> localizer)
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage(_ => localizer["Validation:ProductRequired"]);

        RuleFor(x => x.OrderedQuantity)
            .GreaterThan(0).WithMessage(_ => localizer["Validation:OrderedQuantityPositive"]);

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:UnitPriceNonNegative"]);
    }
}

public class UpdatePurchaseOrderItemDtoValidator : AbstractValidator<UpdatePurchaseOrderItemDto>
{
    public UpdatePurchaseOrderItemDtoValidator(IStringLocalizer<OperationsResource> localizer)
    {
        RuleFor(x => x.OrderedQuantity)
            .GreaterThan(0).WithMessage(_ => localizer["Validation:OrderedQuantityPositive"]);

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:UnitPriceNonNegative"]);
    }
}

public class ReceiveItemsDtoValidator : AbstractValidator<ReceiveItemsDto>
{
    public ReceiveItemsDtoValidator(IStringLocalizer<OperationsResource> localizer)
    {
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage(_ => localizer["Validation:ReceiveLinesRequired"])
            .Must(lines => lines.Select(l => l.ItemId).Distinct().Count() == lines.Count)
            .WithMessage(_ => localizer["Validation:ReceiveLinesUnique"]);

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId)
                .NotEmpty().WithMessage(_ => localizer["Validation:ReceiveItemRequired"]);

            line.RuleFor(l => l.ReceivedQuantity)
                .GreaterThan(0).WithMessage(_ => localizer["Validation:ReceiveQuantityPositive"]);
        });
    }
}
