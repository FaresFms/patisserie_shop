using System;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using NSubstitute;
using Production;
using Production.Entities;
using Production.Formulas;
using Production.Orders;
using Production.Plans;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
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

    [Fact]
    public void Confirmed_plan_moves_through_in_progress_and_closed()
    {
        var plan = NewPlan();
        plan.AddLine(
            Guid.NewGuid(), Guid.NewGuid(), 5, 0, 0, 5, 5,
            1m, 1m, 1m, 3m);
        plan.Confirm(Guid.NewGuid());

        plan.MarkInProgress();
        plan.Status.ShouldBe(ProductionPlanStatuses.InProgress);

        plan.Close();
        plan.Status.ShouldBe(ProductionPlanStatuses.Closed);
    }

    [Fact]
    public void Confirmed_plan_with_orders_cannot_be_cancelled()
    {
        var plan = NewPlan();
        plan.AddLine(
            Guid.NewGuid(), Guid.NewGuid(), 5, 0, 0, 5, 5,
            1m, 1m, 1m, 3m);
        plan.Confirm(Guid.NewGuid());

        Should.Throw<BusinessException>(() => plan.Cancel(hasProductionOrders: true))
            .Code.ShouldBe(ProductionErrorCodes.CannotCancelPlanWithOrders);
    }

    [Fact]
    public async Task Manager_rejects_missing_formula_before_changing_plan_status()
    {
        var plan = NewPlan();
        var productId = Guid.NewGuid();
        plan.AddLine(
            Guid.NewGuid(), productId, 5, 0, 0, 5, 5,
            0m, 0m, 0m, 0m);

        var formulaRepository = Substitute.For<IProductionFormulaRepository>();
        formulaRepository
            .GetActiveDefaultForProductAsync(productId, Arg.Any<CancellationToken>())
            .Returns((AppProductionFormula?)null);

        var manager = new ProductionPlanManager(
            Substitute.For<IRepository<AppBranch, Guid>>(),
            Substitute.For<IProductionPlanRepository>(),
            Substitute.For<IProductionOrderRepository>(),
            formulaRepository);

        var exception = await Should.ThrowAsync<BusinessException>(
            () => manager.ConfirmAsync(plan, Guid.NewGuid()));

        exception.Code.ShouldBe(ProductionErrorCodes.FormulaRequiredForProductionOrder);
        plan.Status.ShouldBe(ProductionPlanStatuses.Draft);
    }
}
