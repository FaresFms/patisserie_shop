using System;
using System.Collections.Generic;

namespace Production.Formulas;

/// <summary>One ingredient breakdown line of a planned-cost preview.</summary>
public class PlannedCostLineDto
{
    public Guid IngredientProductId { get; set; }
    public string? IngredientProductName { get; set; }
    public string? IngredientUnit { get; set; }
    public int RequiredQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineCost { get; set; }
    public bool HasZeroCost { get; set; }
}

/// <summary>
/// Deterministic planned-cost result for a formula at a target output quantity.
/// Mirrors <c>Production.Costing.ProductionCostResult</c>. FROZEN CONTRACT.
/// </summary>
public class PlannedCostDto
{
    public Guid FormulaId { get; set; }
    public int PlannedOutputQuantity { get; set; }
    public int Batches { get; set; }
    public decimal PlannedIngredientCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal OverheadCost { get; set; }
    public decimal PlannedTotalCost { get; set; }
    public decimal PlannedUnitCost { get; set; }
    public string Currency { get; set; } = "USD";
    public bool HasZeroCostIngredient { get; set; }
    public List<PlannedCostLineDto> Lines { get; set; } = new();
}
