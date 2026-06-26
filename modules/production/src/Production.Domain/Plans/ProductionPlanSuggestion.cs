using System;

namespace Production.Plans;

public class ProductionPlanSuggestion
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductSku { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public int RequestedQuantity { get; set; }
    public int ForecastQuantity { get; set; }
    public int CurrentKitchenStock { get; set; }
    public int SuggestedQuantity { get; set; }
    public decimal EstimatedIngredientCost { get; set; }
    public decimal EstimatedLaborCost { get; set; }
    public decimal EstimatedOverheadCost { get; set; }
    public decimal EstimatedTotalCost { get; set; }
    public bool HasDefaultFormula { get; set; }
    public bool HasZeroCostIngredient { get; set; }
}
