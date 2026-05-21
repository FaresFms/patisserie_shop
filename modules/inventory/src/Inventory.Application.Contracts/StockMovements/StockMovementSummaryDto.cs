namespace Inventory.StockMovements;

public class StockMovementSummaryDto
{
    public int TotalCount { get; set; }
    public int PurchaseCount { get; set; }
    public int SaleCount { get; set; }
    public int TransferInCount { get; set; }
    public int TransferOutCount { get; set; }
    public int ManualAdjustmentCount { get; set; }
}
