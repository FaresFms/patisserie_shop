using System;
using System.Collections.Generic;
using Production.Entities;
using Volo.Abp;

namespace Production.Costing;

/// <summary>
/// Pure, deterministic planned-cost calculator for a production formula. No database,
/// no AI/ML, no dynamic expressions — the caller supplies ingredient unit costs. Batch
/// scaling and per-ingredient quantities round UP (ceil) to whole base units, because
/// you cannot buy/consume a fraction of a base unit. Money/percentages are decimals.
///
/// Formula (FROZEN — later waves depend on it):
///   batches      = ceil(plannedOutputQuantity / formula.OutputQuantity), min 1
///   requiredQty  = ceil(plannedOutputQuantity / formula.OutputQuantity
///                       * item.Quantity * (1 + item.LossPercent/100))   [integer base units]
///   lineCost     = requiredQty * unitCost
///   ingredient   = Σ lineCost
///   labor        = formula.LaborCostPerBatch    * batches
///   overhead     = formula.OverheadCostPerBatch * batches
///   total        = ingredient + labor + overhead
///   unitCost     = total / plannedOutputQuantity
/// </summary>
public static class ProductionCostCalculator
{
    /// <param name="formula">The recipe (with its items loaded).</param>
    /// <param name="plannedOutputQuantity">Desired finished-unit output. Must be &gt; 0.</param>
    /// <param name="ingredientUnitCosts">
    /// Per-ingredient unit cost (this wave: the product's CostPrice). Missing entries are
    /// treated as zero cost and flagged via <see cref="ProductionCostLine.HasZeroCost"/>.
    /// </param>
    public static ProductionCostResult Calculate(
        AppProductionFormula formula,
        int plannedOutputQuantity,
        IReadOnlyDictionary<Guid, decimal> ingredientUnitCosts)
    {
        Check.NotNull(formula, nameof(formula));
        Check.NotNull(ingredientUnitCosts, nameof(ingredientUnitCosts));

        if (plannedOutputQuantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidPlannedOutputQuantity)
                .WithData("PlannedOutputQuantity", plannedOutputQuantity);
        }

        // Real (fractional) batch multiplier, kept as decimal so the per-ingredient
        // ceil happens once on the final required quantity (not per integer batch).
        var batchMultiplier = (decimal)plannedOutputQuantity / formula.OutputQuantity;

        // Whole batches to run (labor/overhead are charged per started batch).
        var batches = (int)Math.Ceiling(batchMultiplier);
        if (batches < 1)
        {
            batches = 1;
        }

        var lines = new List<ProductionCostLine>(formula.Items.Count);
        var ingredientCost = 0m;

        foreach (var item in formula.Items)
        {
            var lossFactor = 1m + (item.LossPercent / 100m);
            var rawRequired = batchMultiplier * item.Quantity * lossFactor;
            var requiredQty = (int)Math.Ceiling(rawRequired);

            ingredientUnitCosts.TryGetValue(item.IngredientProductId, out var unitCost);
            var hasZeroCost = unitCost <= 0m;
            var lineCost = requiredQty * unitCost;

            ingredientCost += lineCost;
            lines.Add(new ProductionCostLine(
                item.IngredientProductId,
                requiredQty,
                unitCost,
                lineCost,
                hasZeroCost));
        }

        var laborCost = formula.LaborCostPerBatch * batches;
        var overheadCost = formula.OverheadCostPerBatch * batches;
        var totalCost = ingredientCost + laborCost + overheadCost;
        var unitCostResult = totalCost / plannedOutputQuantity;

        return new ProductionCostResult(
            plannedOutputQuantity,
            batches,
            lines,
            ingredientCost,
            laborCost,
            overheadCost,
            totalCost,
            unitCostResult);
    }
}
