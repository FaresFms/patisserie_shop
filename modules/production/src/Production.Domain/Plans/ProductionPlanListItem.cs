using System;

namespace Production.Plans;

public class ProductionPlanListItem
{
    public Guid Id { get; set; }
    public string PlanNumber { get; set; } = null!;
    public Guid KitchenBranchId { get; set; }
    public string KitchenBranchName { get; set; } = null!;
    public DateTime ProductionDate { get; set; }
    public string Status { get; set; } = ProductionPlanStatuses.Draft;
    public int LineCount { get; set; }
    public int SuggestedTotalQuantity { get; set; }
    public int PlannedTotalQuantity { get; set; }
    public decimal EstimatedTotalCost { get; set; }
}
