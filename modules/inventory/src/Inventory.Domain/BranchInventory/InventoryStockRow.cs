using System;
using Inventory.Entities;

namespace Inventory.BranchInventory;

public class InventoryStockRow
{
    public AppBranchInventory Inventory { get; set; } = null!;
    public AppProduct Product { get; set; } = null!;
}
