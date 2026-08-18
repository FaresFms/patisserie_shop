using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Production.Entities;

public class AppProductionOrderIngredient : Entity<Guid>
{
    public Guid ProductionOrderId { get; private set; }
    public Guid IngredientProductId { get; private set; }
    public int RequiredQuantity { get; private set; }
    public int ConsumedQuantity { get; private set; }
    public decimal UnitCostSnapshot { get; private set; }
    public decimal TotalCost { get; private set; }

    protected AppProductionOrderIngredient() { }

    internal AppProductionOrderIngredient(
        Guid id,
        Guid productionOrderId,
        Guid ingredientProductId,
        int requiredQuantity,
        decimal unitCostSnapshot)
        : base(id)
    {
        ProductionOrderId = productionOrderId;
        IngredientProductId = ingredientProductId;
        SetRequiredQuantity(requiredQuantity);
        SetCostSnapshot(unitCostSnapshot);
    }

    internal void MarkConsumed()
    {
        ConsumedQuantity = RequiredQuantity;
        TotalCost = RequiredQuantity * UnitCostSnapshot;
    }

    internal void RefreshCostSnapshot(decimal unitCostSnapshot)
    {
        if (ConsumedQuantity > 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderStatusTransition);
        }

        SetCostSnapshot(unitCostSnapshot);
    }

    internal void RecordActualCost(decimal actualTotalCost)
    {
        if (ConsumedQuantity <= 0 || actualTotalCost < 0m)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidCost);
        }

        UnitCostSnapshot = actualTotalCost / ConsumedQuantity;
        TotalCost = actualTotalCost;
    }

    private void SetRequiredQuantity(int requiredQuantity)
    {
        if (requiredQuantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity)
                .WithData("RequiredQuantity", requiredQuantity);
        }

        RequiredQuantity = requiredQuantity;
    }

    private void SetCostSnapshot(decimal unitCostSnapshot)
    {
        if (unitCostSnapshot < 0m)
        {
            unitCostSnapshot = 0m;
        }

        UnitCostSnapshot = unitCostSnapshot;
        TotalCost = ConsumedQuantity * UnitCostSnapshot;
    }
}
