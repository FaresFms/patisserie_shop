using System;

namespace Production.BranchRequests;

public class BranchProductionRequestStockDispatchTarget
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
