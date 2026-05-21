namespace Inventory.BranchInventory;

public class AdjustStockDto
{
    public int NewQuantity { get; set; }
    public string MovementType { get; set; } = StockMovementTypes.ManualAdjustment;
    public string? Notes { get; set; }
    public string? ConcurrencyStamp { get; set; }
}
