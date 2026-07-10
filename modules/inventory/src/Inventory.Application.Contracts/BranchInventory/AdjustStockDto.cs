using System;

namespace Inventory.BranchInventory;

public class AdjustStockDto
{
    public int NewQuantity { get; set; }
    public string MovementType { get; set; } = StockMovementTypes.ManualAdjustment;
    public string? Notes { get; set; }

    /// <summary>
    /// When the goods were made/cooked. Only used when stock INCREASES on a
    /// perishable product: the new batch expires at ProductionDate + shelf life
    /// instead of today + shelf life.
    /// </summary>
    public DateTime? ProductionDate { get; set; }

    public string? ConcurrencyStamp { get; set; }
}
