using Inventory.Entities;

namespace Inventory.BranchInventory;

public class BranchInventoryWithProduct
{
    public AppBranchInventory Inventory { get; set; } = null!;
    public AppProduct Product { get; set; } = null!;
}

public class StockSnapshot
{
    public int QuantityOnHand { get; set; }
    public int MinimumStock { get; set; }
}
