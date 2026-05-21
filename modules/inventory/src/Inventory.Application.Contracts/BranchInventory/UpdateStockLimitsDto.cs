namespace Inventory.BranchInventory;

public class UpdateStockLimitsDto
{
    public int MinimumStock { get; set; }
    public int? MaximumStock { get; set; }
    public string? ConcurrencyStamp { get; set; }
}
