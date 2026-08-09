using System;

namespace Production.Plans;

public class ProductionPlanLineDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductSku { get; set; }
    public string? ProductUnit { get; set; }
    public int RequestedQuantity { get; set; }
    public int ForecastQuantity { get; set; }
    public int CurrentKitchenStock { get; set; }
    public int SuggestedQuantity { get; set; }
    public int PlannedQuantity { get; set; }
    public string? OverrideReason { get; set; }
    public decimal EstimatedIngredientCost { get; set; }
    public decimal EstimatedLaborCost { get; set; }
    public decimal EstimatedOverheadCost { get; set; }
    public decimal EstimatedTotalCost { get; set; }
    public bool HasActiveDefaultFormula { get; set; }
}
