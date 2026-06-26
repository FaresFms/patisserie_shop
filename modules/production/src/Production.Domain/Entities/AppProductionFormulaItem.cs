using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Production.Entities;

/// <summary>
/// A single ingredient line of an <see cref="AppProductionFormula"/>. This is a CHILD
/// entity of the formula aggregate — it has NO repository and is only ever created,
/// mutated or removed through the root. Quantities are base integer units (e.g. grams).
/// </summary>
public class AppProductionFormulaItem : Entity<Guid>
{
    public Guid FormulaId { get; private set; }
    public Guid IngredientProductId { get; private set; }

    /// <summary>How much of the ingredient one full formula batch consumes, in base units.</summary>
    public int Quantity { get; private set; }

    /// <summary>Extra per-ingredient loss (trim, spillage) added on top of the base quantity, 0–100.</summary>
    public decimal LossPercent { get; private set; }

    public int SortOrder { get; private set; }

    protected AppProductionFormulaItem() { }

    internal AppProductionFormulaItem(
        Guid id,
        Guid formulaId,
        Guid ingredientProductId,
        int quantity,
        decimal lossPercent,
        int sortOrder)
        : base(id)
    {
        FormulaId = formulaId;
        IngredientProductId = ingredientProductId;
        SetQuantity(quantity);
        SetLossPercent(lossPercent);
        SortOrder = sortOrder;
    }

    internal void SetQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidItemQuantity)
                .WithData("Quantity", quantity);
        }
        Quantity = quantity;
    }

    internal void SetLossPercent(decimal lossPercent)
    {
        if (lossPercent < 0m || lossPercent > 100m)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidItemLossPercent)
                .WithData("LossPercent", lossPercent);
        }
        LossPercent = lossPercent;
    }

    internal void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
}
