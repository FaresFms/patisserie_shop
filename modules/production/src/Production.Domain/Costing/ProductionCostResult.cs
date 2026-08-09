using System;
using System.Collections.Generic;

namespace Production.Costing;

/// <summary>
/// One ingredient line of a planned-cost computation. Quantities are base integer
/// units; <see cref="UnitCost"/> / <see cref="LineCost"/> are money decimals.
/// FROZEN CONTRACT — later waves depend on this shape.
/// </summary>
public sealed record ProductionCostLine(
    Guid IngredientProductId,
    int RequiredQuantity,
    decimal UnitCost,
    decimal LineCost,
    bool HasZeroCost);

/// <summary>
/// Deterministic result of <see cref="ProductionCostCalculator"/> for a target output
/// quantity. All amounts are money decimals; quantities are base integer units.
/// FROZEN CONTRACT — later waves depend on this shape.
/// </summary>
public sealed record ProductionCostResult(
    int PlannedOutputQuantity,
    int ExpectedGrossOutputQuantity,
    int Batches,
    IReadOnlyList<ProductionCostLine> Lines,
    decimal PlannedIngredientCost,
    decimal LaborCost,
    decimal OverheadCost,
    decimal PlannedTotalCost,
    decimal PlannedUnitCost)
{
    /// <summary>True when at least one ingredient had a zero unit cost (UI should warn).</summary>
    public bool HasZeroCostIngredient
    {
        get
        {
            foreach (var line in Lines)
            {
                if (line.HasZeroCost)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
