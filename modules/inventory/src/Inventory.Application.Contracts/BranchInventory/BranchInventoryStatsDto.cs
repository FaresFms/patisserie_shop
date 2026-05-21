namespace Inventory.BranchInventory;

public class BranchInventoryStatsDto
{
    public int TotalItems { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public int HealthyStockCount { get; set; }
}
