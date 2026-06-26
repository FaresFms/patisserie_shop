using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Production.Entities;

public class AppProductionPlanLine : Entity<Guid>
{
    public Guid PlanId { get; private set; }
    public Guid ProductId { get; private set; }
    public int RequestedQuantity { get; private set; }
    public int ForecastQuantity { get; private set; }
    public int CurrentKitchenStock { get; private set; }
    public int SuggestedQuantity { get; private set; }
    public int PlannedQuantity { get; private set; }
    public string? OverrideReason { get; private set; }
    public decimal EstimatedIngredientCost { get; private set; }
    public decimal EstimatedLaborCost { get; private set; }
    public decimal EstimatedOverheadCost { get; private set; }
    public decimal EstimatedTotalCost { get; private set; }

    protected AppProductionPlanLine() { }

    internal AppProductionPlanLine(
        Guid id,
        Guid planId,
        Guid productId,
        int requestedQuantity,
        int forecastQuantity,
        int currentKitchenStock,
        int suggestedQuantity,
        int plannedQuantity,
        decimal estimatedIngredientCost,
        decimal estimatedLaborCost,
        decimal estimatedOverheadCost,
        decimal estimatedTotalCost)
        : base(id)
    {
        PlanId = planId;
        ProductId = productId;
        RequestedQuantity = Math.Max(0, requestedQuantity);
        ForecastQuantity = Math.Max(0, forecastQuantity);
        CurrentKitchenStock = Math.Max(0, currentKitchenStock);
        SuggestedQuantity = Math.Max(0, suggestedQuantity);
        SetPlannedQuantity(plannedQuantity, null);
        SetCostEstimate(estimatedIngredientCost, estimatedLaborCost, estimatedOverheadCost, estimatedTotalCost);
    }

    internal void SetPlannedQuantity(int plannedQuantity, string? overrideReason)
    {
        if (plannedQuantity < 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidPlannedQuantity)
                .WithData("PlannedQuantity", plannedQuantity);
        }
        if (plannedQuantity != SuggestedQuantity && string.IsNullOrWhiteSpace(overrideReason))
        {
            throw new BusinessException(ProductionErrorCodes.PlanOverrideReasonRequired);
        }

        PlannedQuantity = plannedQuantity;
        OverrideReason = plannedQuantity == SuggestedQuantity ? null : overrideReason?.Trim();
    }

    internal void SetCostEstimate(
        decimal ingredientCost,
        decimal laborCost,
        decimal overheadCost,
        decimal totalCost)
    {
        EstimatedIngredientCost = Math.Max(0m, ingredientCost);
        EstimatedLaborCost = Math.Max(0m, laborCost);
        EstimatedOverheadCost = Math.Max(0m, overheadCost);
        EstimatedTotalCost = Math.Max(0m, totalCost);
    }
}
