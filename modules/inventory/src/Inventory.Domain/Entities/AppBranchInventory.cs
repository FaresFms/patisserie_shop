using System;
using Inventory.Events;
using Volo.Abp.Domain.Entities.Auditing;

namespace Inventory.Entities;

public class AppBranchInventory : AuditedAggregateRoot<Guid>
{
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public int QuantityOnHand { get; set; }
    public int MinimumStock { get; set; }
    public int? MaximumStock { get; set; }
    public DateTime? LastRestockedDate { get; set; }
    public DateTime? LastSoldDate { get; set; }

    protected AppBranchInventory() { }

    public AppBranchInventory(
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

    public void UpdateStock(int newQty)
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
