using System;
using System.Collections.Generic;

namespace Inventory.BranchInventory;

public class TransferSourceRequestLine
{
    public Guid ProductId { get; set; }
    public int RequestedQuantity { get; set; }
}

public class TransferSourceRecommendation
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public bool IsRecommended { get; set; }
    public bool CanFulfillAll { get; set; }
    public int CoveredItemCount { get; set; }
    public int TotalItemCount { get; set; }
    public int CoveragePercent { get; set; }
    public int TotalSafeAvailable { get; set; }
    public int TotalMissingQuantity { get; set; }
    public List<TransferSourceRecommendationLine> Products { get; set; } = new();
}

public class TransferSourceRecommendationLine
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public int QuantityOnHand { get; set; }
    public int MinimumStock { get; set; }
    public int SafeAvailableQuantity { get; set; }
    public int CoveredQuantity { get; set; }
    public int MissingQuantity { get; set; }
}
