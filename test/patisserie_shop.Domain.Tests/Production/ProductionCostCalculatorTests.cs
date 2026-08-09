using System;
using System.Collections.Generic;
using System.Linq;
using Production;
using Production.Costing;
using Production.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace patisserie_shop.Production;

/// <summary>
/// Pure unit tests for the deterministic <see cref="ProductionCostCalculator"/>: batch
/// scaling, ceil rounding, per-ingredient loss, and the ingredient + labor×batches +
/// overhead×batches total / unit-cost math. No DB — the caller supplies unit costs.
/// </summary>
public class ProductionCostCalculatorTests
{
    /// <summary>
    /// Builds a formula via its internal ctor (visible to this test assembly through
    /// InternalsVisibleTo) and appends the supplied ingredient items.
    /// </summary>
    private static AppProductionFormula NewFormula(
        int outputQuantity,
        decimal laborCostPerBatch = 0m,
        decimal overheadCostPerBatch = 0m,
        decimal expectedWastePercent = 0m,
        params (Guid id, int qty, decimal loss)[] items)
    {
        var formula = new AppProductionFormula(
            Guid.NewGuid(),
            finishedProductId: Guid.NewGuid(),
            formulaName: "Test Formula",
            outputQuantity: outputQuantity,
            version: 1,
            expectedWastePercent: expectedWastePercent,
            laborCostPerBatch: laborCostPerBatch,
            overheadCostPerBatch: overheadCostPerBatch,
            estimatedProductionMinutes: 0);

        var sort = 0;
        foreach (var (id, qty, loss) in items)
        {
            formula.AddItem(Guid.NewGuid(), id, qty, loss, sort++);
        }
        return formula;
    }

    [Fact]
    public void Doubling_Output_Exactly_Doubles_Ingredient_With_No_Loss()
    {
        // Formula yields 100; one ingredient 50 base units per batch, no loss.
        var ing = Guid.NewGuid();
        var formula = NewFormula(100, items: (ing, 50, 0m));

        // Plan 200 finished units → exactly 2× the ingredient.
        var result = ProductionCostCalculator.Calculate(
            formula, 200, new Dictionary<Guid, decimal> { [ing] = 2m });

        result.Batches.ShouldBe(2);
        var line = result.Lines.ShouldHaveSingleItem();
        line.RequiredQuantity.ShouldBe(100); // 2 × 50
        line.UnitCost.ShouldBe(2m);
        line.LineCost.ShouldBe(200m);        // 100 × 2
        line.HasZeroCost.ShouldBeFalse();
    }

    [Fact]
    public void Loss_Percent_Inflates_Required_Quantity_And_Ceils()
    {
        var ing = Guid.NewGuid();
        var formula = NewFormula(100, items: (ing, 50, 10m)); // 10% loss

        var result = ProductionCostCalculator.Calculate(
            formula, 200, new Dictionary<Guid, decimal> { [ing] = 1m });

        // 2 × 50 × 1.10 = 110 (whole already).
        result.Lines.Single().RequiredQuantity.ShouldBe(110);
    }

    [Fact]
    public void Required_Quantity_Rounds_Up_To_Whole_Base_Units()
    {
        var ing = Guid.NewGuid();
        // Output 3, plan 4 → batchMultiplier 4/3. Item 10 units → 13.33.. → ceil 14.
        var formula = NewFormula(3, items: (ing, 10, 0m));

        var result = ProductionCostCalculator.Calculate(
            formula, 4, new Dictionary<Guid, decimal> { [ing] = 1m });

        result.Batches.ShouldBe(2);                 // ceil(4/3)
        result.Lines.Single().RequiredQuantity.ShouldBe(14); // ceil(40/3)
    }

    [Fact]
    public void Total_Cost_Is_Ingredients_Plus_Labor_And_Overhead_Times_Batches()
    {
        var ingA = Guid.NewGuid();
        var ingB = Guid.NewGuid();
        var formula = NewFormula(
            outputQuantity: 100,
            laborCostPerBatch: 10m,
            overheadCostPerBatch: 5m,
            items: new[] { (ingA, 50, 0m), (ingB, 20, 0m) });

        var costs = new Dictionary<Guid, decimal> { [ingA] = 2m, [ingB] = 3m };

        // Plan 200 → 2 batches.
        var result = ProductionCostCalculator.Calculate(formula, 200, costs);

        // Ingredient cost: (100×2) + (40×3) = 200 + 120 = 320.
        result.PlannedIngredientCost.ShouldBe(320m);
        result.LaborCost.ShouldBe(20m);     // 10 × 2 batches
        result.OverheadCost.ShouldBe(10m);  // 5 × 2 batches
        result.PlannedTotalCost.ShouldBe(350m);
        // Unit cost: 350 / 200 = 1.75
        result.PlannedUnitCost.ShouldBe(1.75m);
    }

    [Fact]
    public void Batches_Is_At_Least_One_When_Planned_Is_Below_One_Batch()
    {
        var ing = Guid.NewGuid();
        var formula = NewFormula(100, laborCostPerBatch: 10m, items: (ing, 50, 0m));

        // Plan 10 of a 100-yield formula → still one batch of labor/overhead.
        var result = ProductionCostCalculator.Calculate(
            formula, 10, new Dictionary<Guid, decimal> { [ing] = 1m });

        result.Batches.ShouldBe(1);
        result.LaborCost.ShouldBe(10m);
        // requiredQty = ceil(10/100 × 50) = ceil(5) = 5
        result.Lines.Single().RequiredQuantity.ShouldBe(5);
    }

    [Fact]
    public void Expected_Output_Waste_Inflates_Gross_Output_And_Ingredients()
    {
        var ing = Guid.NewGuid();
        var formula = NewFormula(
            outputQuantity: 100,
            expectedWastePercent: 10m,
            items: (ing, 50, 0m));

        var result = ProductionCostCalculator.Calculate(
            formula, 90, new Dictionary<Guid, decimal> { [ing] = 2m });

        result.PlannedOutputQuantity.ShouldBe(90);
        result.ExpectedGrossOutputQuantity.ShouldBe(100);
        result.Batches.ShouldBe(1);
        result.Lines.Single().RequiredQuantity.ShouldBe(50);
        result.PlannedIngredientCost.ShouldBe(100m);
    }

    [Fact]
    public void Zero_Cost_Ingredient_Is_Flagged_But_Still_Computed()
    {
        var ing = Guid.NewGuid();
        var formula = NewFormula(100, items: (ing, 50, 0m));

        // No cost supplied → treated as zero.
        var result = ProductionCostCalculator.Calculate(
            formula, 100, new Dictionary<Guid, decimal>());

        var line = result.Lines.Single();
        line.RequiredQuantity.ShouldBe(50);
        line.UnitCost.ShouldBe(0m);
        line.LineCost.ShouldBe(0m);
        line.HasZeroCost.ShouldBeTrue();
        result.HasZeroCostIngredient.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_Non_Positive_Planned_Output_Quantity()
    {
        var formula = NewFormula(100, items: (Guid.NewGuid(), 50, 0m));

        Should.Throw<BusinessException>(() =>
                ProductionCostCalculator.Calculate(formula, 0, new Dictionary<Guid, decimal>()))
            .Code.ShouldBe(ProductionErrorCodes.InvalidPlannedOutputQuantity);
    }
}
