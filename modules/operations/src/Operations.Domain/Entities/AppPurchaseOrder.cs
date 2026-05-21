using System;
using System.Collections.Generic;
using System.Linq;
using Operations.Events;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Operations.Entities;

public class AppPurchaseOrder : FullAuditedAggregateRoot<Guid>
{
    public Guid SupplierId { get; set; }
    public Guid DestBranchId { get; set; }
    public string PONumber { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public DateTime? ActualDeliveryDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Notes { get; set; }

    public ICollection<AppPurchaseOrderItem> Items { get; private set; }

    protected AppPurchaseOrder()
    {
        Items = new List<AppPurchaseOrderItem>();
    }

    public AppPurchaseOrder(
        Guid id,
        Guid supplierId,
        Guid destBranchId,
        string poNumber,
        string status,
        DateTime orderDate,
        DateTime? expectedDeliveryDate = null,
        string currency = "USD",
        string? notes = null)
        : base(id)
    {
        SupplierId = supplierId;
        DestBranchId = destBranchId;
        PONumber = poNumber;
        Status = status;
        OrderDate = orderDate;
        ExpectedDeliveryDate = expectedDeliveryDate;
        Currency = currency;
        Notes = notes;
        Items = new List<AppPurchaseOrderItem>();
        TotalAmount = 0m;
    }

    public AppPurchaseOrderItem AddItem(Guid productId, int orderedQty, decimal unitPrice)
    {
        var item = new AppPurchaseOrderItem(Guid.NewGuid(), Id, productId, orderedQty, unitPrice);
        Items.Add(item);
        TotalAmount = Items.Sum(i => i.Subtotal);
        return item;
    }

    public void ReceiveItem(Guid productId, int receivedQty)
    {
        var item = Items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new BusinessException("Operations:PurchaseOrderItemNotFound");

        item.ReceivedQuantity = receivedQty;

        if (Items.All(i => i.ReceivedQuantity >= i.OrderedQuantity))
        {
            AddDistributedEvent(new PurchaseReceivedEto
            {
                PurchaseOrderId = Id,
                SupplierId = SupplierId,
                DestBranchId = DestBranchId
            });
        }
    }
}
