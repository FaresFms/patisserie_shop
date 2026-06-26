using System;

namespace Production.Orders;

public class ProductionOrderListItem
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public Guid KitchenBranchId { get; set; }
    public string KitchenBranchName { get; set; } = null!;
    public Guid FinishedProductId { get; set; }
    public string FinishedProductName { get; set; } = null!;
    public string FinishedProductSku { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public string Status { get; set; } = ProductionOrderStatuses.Draft;
    public string Priority { get; set; } = ProductionPriorities.Normal;
    public int PlannedOutputQuantity { get; set; }
    public int AcceptedQuantity { get; set; }
    public DateTime? ActualStartTime { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal TotalProductionCost { get; set; }
}
