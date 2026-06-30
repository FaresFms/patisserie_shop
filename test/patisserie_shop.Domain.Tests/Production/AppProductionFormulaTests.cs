using System;
using System.Linq;
using Production;
using Production.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace patisserie_shop.Production;

/// <summary>
/// Pure unit tests for the AppProductionFormula aggregate: constructor invariants
/// (output quantity, name), the ingredient-item guards (quantity, loss percent,
/// duplicate ingredient), and the simple state flags.
/// </summary>
public class AppProductionFormulaTests
{
    private static AppProductionFormula NewFormula(int outputQuantity = 100, string name = "Croissant Dough")
        => new(
            Guid.NewGuid(),
            finishedProductId: Guid.NewGuid(),
            formulaName: name,
            outputQuantity: outputQuantity,
            version: 1,
            expectedWastePercent: 0m,
            laborCostPerBatch: 0m,
            overheadCostPerBatch: 0m,
            estimatedProductionMinutes: 0);

    [Fact]
    public void Ctor_Rejects_Non_Positive_Output_Quantity()
    {
        Should.Throw<BusinessException>(() => NewFormula(outputQuantity: 0))
            .Code.ShouldBe(ProductionErrorCodes.InvalidOutputQuantity);

        Should.Throw<BusinessException>(() => NewFormula(outputQuantity: -5))
            .Code.ShouldBe(ProductionErrorCodes.InvalidOutputQuantity);
    }

    [Fact]
    public void Ctor_Rejects_Empty_Name()
    {
        Should.Throw<BusinessException>(() => NewFormula(name: "  "))
            .Code.ShouldBe(ProductionErrorCodes.FormulaNameRequired);
    }

    [Fact]
    public void New_Formula_Defaults_Are_Active_Not_Default_Version_One()
    {
        var f = NewFormula();
        f.IsActive.ShouldBeTrue();
        f.IsDefault.ShouldBeFalse();
        f.Version.ShouldBe(1);
        f.Items.ShouldBeEmpty();
    }

    [Fact]
    public void AddItem_Rejects_Non_Positive_Quantity()
    {
        var f = NewFormula();
        Should.Throw<BusinessException>(() => f.AddItem(Guid.NewGuid(), Guid.NewGuid(), 0, 0m, 0))
            .Code.ShouldBe(ProductionErrorCodes.InvalidItemQuantity);
    }

    [Fact]
    public void AddItem_Rejects_Out_Of_Range_Loss_Percent()
    {
        var f = NewFormula();
        Should.Throw<BusinessException>(() => f.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10, -1m, 0))
            .Code.ShouldBe(ProductionErrorCodes.InvalidItemLossPercent);
        Should.Throw<BusinessException>(() => f.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10, 101m, 0))
            .Code.ShouldBe(ProductionErrorCodes.InvalidItemLossPercent);
    }

    [Fact]
    public void AddItem_Rejects_Duplicate_Ingredient()
    {
        var f = NewFormula();
        var ing = Guid.NewGuid();
        f.AddItem(Guid.NewGuid(), ing, 10, 0m, 0);

        Should.Throw<BusinessException>(() => f.AddItem(Guid.NewGuid(), ing, 5, 0m, 1))
            .Code.ShouldBe(ProductionErrorCodes.DuplicateIngredient);
    }

    [Fact]
    public void AddItem_RemoveItem_ClearItems_Maintain_Collection()
    {
        var f = NewFormula();
        var item = f.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10, 5m, 0);
        f.AddItem(Guid.NewGuid(), Guid.NewGuid(), 20, 0m, 1);
        f.Items.Count.ShouldBe(2);

        f.RemoveItem(item.Id);
        f.Items.Count.ShouldBe(1);

        Should.Throw<BusinessException>(() => f.RemoveItem(item.Id))
            .Code.ShouldBe(ProductionErrorCodes.FormulaItemNotFound);

        f.ClearItems();
        f.Items.ShouldBeEmpty();
    }

    [Fact]
    public void State_Flag_Methods_Toggle_Correctly()
    {
        var f = NewFormula();

        f.Deactivate();
        f.IsActive.ShouldBeFalse();
        f.Activate();
        f.IsActive.ShouldBeTrue();

        f.MarkDefault();
        f.IsDefault.ShouldBeTrue();
        f.UnmarkDefault();
        f.IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void UpdateInfo_Validates_And_Applies_Fields()
    {
        var f = NewFormula();

        Should.Throw<BusinessException>(() =>
                f.UpdateInfo("Updated", outputQuantity: 50, expectedWastePercent: 150m,
                    laborCostPerBatch: 1m, overheadCostPerBatch: 1m, estimatedProductionMinutes: 5, notes: null))
            .Code.ShouldBe(ProductionErrorCodes.InvalidWastePercent);

        f.UpdateInfo("Updated", outputQuantity: 50, expectedWastePercent: 12.5m,
            laborCostPerBatch: 3m, overheadCostPerBatch: 2m, estimatedProductionMinutes: 5, notes: "n");

        f.FormulaName.ShouldBe("Updated");
        f.OutputQuantity.ShouldBe(50);
        f.ExpectedWastePercent.ShouldBe(12.5m);
        f.LaborCostPerBatch.ShouldBe(3m);
        f.OverheadCostPerBatch.ShouldBe(2m);
        f.EstimatedProductionMinutes.ShouldBe(5);
        f.Notes.ShouldBe("n");
    }
}
