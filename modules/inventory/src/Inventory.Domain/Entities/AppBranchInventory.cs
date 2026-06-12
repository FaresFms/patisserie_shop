using System;
using Inventory.Events;
using Volo.Abp.Domain.Entities.Auditing;

namespace Inventory.Entities;

public class AppBranchInventory : AuditedAggregateRoot<Guid>
{
    public Guid BranchId { get; private set; }
    public Guid ProductId { get; private set; }
    public int QuantityOnHand { get; private set; }
    public int MinimumStock { get; internal set; }
    public int? MaximumStock { get; internal set; }
    public DateTime? LastRestockedDate { get; internal set; }
    public DateTime? LastSoldDate { get; internal set; }

    public bool IsOutOfStock => QuantityOnHand <= 0;
    public bool IsLowStock => QuantityOnHand <= MinimumStock;

    protected AppBranchInventory() { }

    internal AppBranchInventory(
        Guid id,
        Guid branchId,
        Guid productId,
        int quantityOnHand = 0,
        int minimumStock = 0,
        int? maximumStock = null)
        : base(id)
    {
        BranchId = branchId;
        ProductId = productId;
        QuantityOnHand = quantityOnHand;
        MinimumStock = minimumStock;
        MaximumStock = maximumStock;
    }

    internal void UpdateStock(int newQty)
    {
        var old = QuantityOnHand;
        QuantityOnHand = newQty;

        AddDistributedEvent(new StockChangedEto
        {
            BranchId = BranchId,
            ProductId = ProductId,
            OldQty = old,
            NewQty = newQty
        });
    }
}
