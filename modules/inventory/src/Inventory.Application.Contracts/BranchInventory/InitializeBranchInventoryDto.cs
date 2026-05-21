using System;

namespace Inventory.BranchInventory;

public class InitializeBranchInventoryDto
{
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public int InitialQuantity { get; set; }
    public int MinimumStock { get; set; }
    public int? MaximumStock { get; set; }
}
