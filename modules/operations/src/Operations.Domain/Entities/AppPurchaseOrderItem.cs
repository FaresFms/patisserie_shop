using System;
using Volo.Abp.Domain.Entities;

namespace Operations.Entities;

public class AppPurchaseOrderItem : Entity<Guid>
{
    public Guid PurchaseOrderId { get; set; }
    public Guid ProductId { get; set; }
    public int OrderedQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }

    protected AppPurchaseOrderItem() { }

    public AppPurchaseOrderItem(
        Guid id,
        Guid purchaseOrderId,
        Guid productId,
        int orderedQuantity,
        decimal unitPrice)
        : base(id)
    {
        PurchaseOrderId = purchaseOrderId;
        ProductId = productId;
        OrderedQuantity = orderedQuantity;
        UnitPrice = unitPrice;
        ReceivedQuantity = 0;
        Subtotal = orderedQuantity * unitPrice;
    }
}
