using System;
using Production;
using Production.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace patisserie_shop.Production;

public class AppProductionPlanTests
{
    private static AppProductionPlan NewPlan()
    {
        return new AppProductionPlan(
            Guid.NewGuid(),
            "PLAN-TEST",
            Guid.NewGuid(),
            DateTime.Today,
            Guid.NewGuid(),
            "n");
    }

    [Fact]
    public void Override_requires_reason_when_planned_quantity_differs()
    {
        var plan = NewPlan();
        var line = plan.AddLine(
            Guid.NewGuid(),
            Guid.NewGuid(),
            requestedQuantity: 80,
            forecastQuantity: 20,
            currentKitchenStock: 10,
            suggestedQuantity: 90,
            plannedQuantity: 90,
            estimatedIngredientCost: 10m,
            estimatedLaborCost: 2m,
            estimatedOverheadCost: 1m,
            estimatedTotalCost: 13m);

        Should.Throw<BusinessException>(() => plan.UpdateLinePlannedQuantity(line.Id, 100, null))
            .Code.ShouldBe(ProductionErrorCodes.PlanOverrideReasonRequired);
    }

    [Fact]
    public void Confirm_requires_at_least_one_positive_planned_quantity()
    {
        var plan = NewPlan();

        Should.Throw<BusinessException>(() => plan.Confirm(Guid.NewGuid()))
            .Code.ShouldBe(ProductionErrorCodes.CannotConfirmEmptyPlan);
    }

    [Fact]
    public void Confirm_moves_draft_plan_to_confirmed()
    {
        var plan = NewPlan();
        plan.AddLine(
            Guid.NewGuid(),
            Guid.NewGuid(),
            requestedQuantity: 80,
            forecastQuantity: 20,
            currentKitchenStock: 10,
            suggestedQuantity: 90,
            plannedQuantity: 90,
            estimatedIngredientCost: 10m,
            estimatedLaborCost: 2m,
            estimatedOverheadCost: 1m,
            estimatedTotalCost: 13m);

        plan.Confirm(Guid.NewGuid());

        plan.Status.ShouldBe(ProductionPlanStatuses.Confirmed);
        plan.ConfirmedAt.ShouldNotBeNull();
    }
}
