using FluentValidation;
using Operations.PurchaseOrders;

namespace Operations.Validation;

public class CreatePurchaseOrderDtoValidator : AbstractValidator<CreatePurchaseOrderDto>
{
    public CreatePurchaseOrderDtoValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("Supplier is required.");

        RuleFor(x => x.DestBranchId)
            .NotEmpty().WithMessage("Destination branch is required.");

        RuleFor(x => x.OrderDate)
            .NotEmpty().WithMessage("Order date is required.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Length(3).WithMessage("Currency must be exactly 3 characters.")
            .Matches("^[A-Z]{3}$").WithMessage("Currency must be a 3-letter ISO code (uppercase).");

        RuleFor(x => x.Notes)
            .MaximumLength(512).WithMessage("Notes must not exceed 512 characters.");

        RuleFor(x => x)
            .Must(x => !x.ExpectedDeliveryDate.HasValue || x.ExpectedDeliveryDate.Value >= x.OrderDate)
            .WithMessage("Expected delivery date cannot be earlier than order date.");
    }
}

public class UpdatePurchaseOrderHeaderDtoValidator : AbstractValidator<UpdatePurchaseOrderHeaderDto>
{
    public UpdatePurchaseOrderHeaderDtoValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("Supplier is required.");

        RuleFor(x => x.DestBranchId)
            .NotEmpty().WithMessage("Destination branch is required.");

        RuleFor(x => x.OrderDate)
            .NotEmpty().WithMessage("Order date is required.");

        RuleFor(x => x.Notes)
            .MaximumLength(512).WithMessage("Notes must not exceed 512 characters.");

        RuleFor(x => x)
            .Must(x => !x.ExpectedDeliveryDate.HasValue || x.ExpectedDeliveryDate.Value >= x.OrderDate)
            .WithMessage("Expected delivery date cannot be earlier than order date.");
    }
}

public class AddPurchaseOrderItemDtoValidator : AbstractValidator<AddPurchaseOrderItemDto>
{
    public AddPurchaseOrderItemDtoValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product is required.");

        RuleFor(x => x.OrderedQuantity)
            .GreaterThan(0).WithMessage("Ordered quantity must be greater than 0.");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Unit price must be 0 or greater.");
    }
}

public class UpdatePurchaseOrderItemDtoValidator : AbstractValidator<UpdatePurchaseOrderItemDto>
{
    public UpdatePurchaseOrderItemDtoValidator()
    {
        RuleFor(x => x.OrderedQuantity)
            .GreaterThan(0).WithMessage("Ordered quantity must be greater than 0.");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Unit price must be 0 or greater.");
    }
}

public class ReceiveItemsDtoValidator : AbstractValidator<ReceiveItemsDto>
{
    public ReceiveItemsDtoValidator()
    {
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("At least one line must be specified.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId)
                .NotEmpty().WithMessage("Item is required.");

            line.RuleFor(l => l.ReceivedQuantity)
                .GreaterThanOrEqualTo(0).WithMessage("Received quantity must be 0 or greater.");
        });
    }
}
