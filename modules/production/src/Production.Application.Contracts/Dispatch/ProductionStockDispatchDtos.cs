using System;

namespace Production.Dispatch;

public class GetProductionStockDispatchQueueInput
{
    public Guid KitchenBranchId { get; set; }
    public string? Filter { get; set; }
}

public class ProductionStockDispatchQueueItemDto
{
    public Guid RequestId { get; set; }
    public Guid RequestItemId { get; set; }
    public string RequestNumber { get; set; } = null!;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = null!;
    public DateTime NeededByDate { get; set; }
    public string Priority { get; set; } = null!;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductSku { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public int ApprovedQuantity { get; set; }
    public int PlannedQuantity { get; set; }
    public int FulfilledQuantity { get; set; }
    public int RemainingUnplannedQuantity { get; set; }
    public int UsableKitchenStock { get; set; }
    public int CommittedKitchenStock { get; set; }
    public int AvailableUncommittedKitchenStock { get; set; }
    public int DispatchableQuantity { get; set; }
}

public class CreateProductionRequestStockTransferDto
{
    public Guid KitchenBranchId { get; set; }
    public Guid RequestId { get; set; }
    public Guid RequestItemId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}
