using System;

namespace Inventory.BranchInventory;

public class ProductBranchStockDto
{
    public Guid BranchId { get; set; }

    public string BranchName { get; set; } = null!;

    public int QuantityOnHand { get; set; }

    public int MinimumStock { get; set; }

    public int? MaximumStock { get; set; }

    public string ProductUnit { get; set; } = null!;

    public bool IsLowStock => QuantityOnHand <= MinimumStock;

    public bool IsOutOfStock => QuantityOnHand <= 0;
}
