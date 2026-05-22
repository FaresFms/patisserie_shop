using FluentValidation;
using Operations.PurchaseOrders;

namespace Operations.Validation;

public class CreatePurchaseOrderDtoValidator : AbstractValidator<CreatePurchaseOrderDto>
{
    public CreatePurchaseOrderDtoValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.DestBranchId).NotEmpty();
        RuleFor(x => x.OrderDate).NotEmpty();
        RuleFor(x => x.Currency).NotEmpty().Length(3).Matches("^[A-Z]{3}$");
        RuleFor(x => x.Notes).MaximumLength(512);
        RuleFor(x => x)
            .Must(x => !x.ExpectedDeliveryDate.HasValue || x.ExpectedDeliveryDate.Value >= x.OrderDate)
            .WithMessage("Expected delivery date cannot be earlier than order date.");
    }
}

public class UpdatePurchaseOrderHeaderDtoValidator : AbstractValidator<UpdatePurchaseOrderHeaderDto>
{
    public UpdatePurchaseOrderHeaderDtoValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.DestBranchId).NotEmpty();
        RuleFor(x => x.OrderDate).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(512);
        RuleFor(x => x)
            .Must(x => !x.ExpectedDeliveryDate.HasValue || x.ExpectedDeliveryDate.Value >= x.OrderDate)
            .WithMessage("Expected delivery date cannot be earlier than order date.");
    }
}

public class AddPurchaseOrderItemDtoValidator : AbstractValidator<AddPurchaseOrderItemDto>
{
    public AddPurchaseOrderItemDtoValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.OrderedQuantity).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
    }
}

public class UpdatePurchaseOrderItemDtoValidator : AbstractValidator<UpdatePurchaseOrderItemDto>
{
    public UpdatePurchaseOrderItemDtoValidator()
    {
        RuleFor(x => x.OrderedQuantity).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
    }
}

public class ReceiveItemsDtoValidator : AbstractValidator<ReceiveItemsDto>
{
    public ReceiveItemsDtoValidator()
    {
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line must be specified.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).NotEmpty();
            line.RuleFor(l => l.ReceivedQuantity).GreaterThanOrEqualTo(0);
        });
    }
}
