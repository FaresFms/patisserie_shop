using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Operations.Entities;

public class AppPurchaseOrderItem : Entity<Guid>
{
    public Guid PurchaseOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int OrderedQuantity { get; private set; }
    public int ReceivedQuantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Subtotal { get; private set; }

    public bool IsFullyReceived => ReceivedQuantity >= OrderedQuantity;

    protected AppPurchaseOrderItem() { }

    internal AppPurchaseOrderItem(
        Guid id,
        Guid purchaseOrderId,
        Guid productId,
        int orderedQuantity,
        decimal unitPrice)
        : base(id)
    {
        if (orderedQuantity <= 0)
            throw new BusinessException(OperationsErrorCodes.InvalidQuantity);
        if (unitPrice < 0)
            throw new BusinessException(OperationsErrorCodes.InvalidPrice);

        PurchaseOrderId = purchaseOrderId;
        ProductId = productId;
        OrderedQuantity = orderedQuantity;
        UnitPrice = unitPrice;
        ReceivedQuantity = 0;
        Subtotal = orderedQuantity * unitPrice;
    }

    internal void ChangeOrderedQuantity(int orderedQuantity)
    {
        if (orderedQuantity <= 0)
            throw new BusinessException(OperationsErrorCodes.InvalidQuantity);
        OrderedQuantity = orderedQuantity;
        Subtotal = orderedQuantity * UnitPrice;
    }

    internal void ChangeUnitPrice(decimal unitPrice)
    {
        if (unitPrice < 0)
            throw new BusinessException(OperationsErrorCodes.InvalidPrice);
        UnitPrice = unitPrice;
        Subtotal = OrderedQuantity * unitPrice;
    }

    internal int Receive(int additionalReceived)
    {
        if (additionalReceived < 0)
            throw new BusinessException(OperationsErrorCodes.InvalidQuantity);
        var newReceived = ReceivedQuantity + additionalReceived;
        if (newReceived > OrderedQuantity)
        {
            throw new BusinessException(OperationsErrorCodes.ReceivedExceedsOrdered)
                .WithData("ProductId", ProductId)
                .WithData("Ordered", OrderedQuantity)
                .WithData("AlreadyReceived", ReceivedQuantity)
                .WithData("Requested", additionalReceived);
        }
        ReceivedQuantity = newReceived;
        return additionalReceived;
    }
}
