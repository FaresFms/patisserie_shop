using System;

namespace Production.Plans;

public class CreateProductionPlanDto
{
    public Guid KitchenBranchId { get; set; }
    public DateTime ProductionDate { get; set; } = DateTime.Today;
    public string? Notes { get; set; }
}

public class UpdateProductionPlanLineDto
{
    public int PlannedQuantity { get; set; }
    public string? OverrideReason { get; set; }
}
