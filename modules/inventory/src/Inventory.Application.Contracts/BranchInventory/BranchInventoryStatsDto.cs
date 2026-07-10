namespace Inventory.BranchInventory;

public class BranchInventoryStatsDto
{
    public int TotalItems { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public int HealthyStockCount { get; set; }

    /// <summary>Products at this branch that have at least one expired unit on hand.</summary>
    public int ExpiredItemCount { get; set; }

    /// <summary>Total expired units on hand across the branch.</summary>
    public int ExpiredUnitCount { get; set; }
}
