using System;
using System.Linq;
using Production;
using Production.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace patisserie_shop.Production;

public class AppProductionOrderTests
{
    private static AppProductionOrder NewOrder(int plannedOutputQuantity = 20)
    {
        return new AppProductionOrder(
            Guid.NewGuid(),
            "PROD-TEST",
            Guid.NewGuid(),
            productionPlanId: Guid.NewGuid(),
            productionPlanLineId: Guid.NewGuid(),
            finishedProductId: Guid.NewGuid(),
            formulaId: Guid.NewGuid(),
            formulaVersion: 1,
            priority: ProductionPriorities.Normal,
            plannedOutputQuantity: plannedOutputQuantity,
            plannedIngredientCost: 40m,
            laborCost: 4m,
            overheadCost: 6m,
            createdByUserId: Guid.NewGuid(),
            notes: "test");
    }

    [Fact]
    public void Start_consumes_ingredient_snapshots_and_moves_to_in_production()
    {
        var order = NewOrder();
        var flourId = Guid.NewGuid();
        var butterId = Guid.NewGuid();
        order.AddIngredientSnapshot(Guid.NewGuid(), flourId, requiredQuantity: 10, unitCostSnapshot: 2m);
        order.AddIngredientSnapshot(Guid.NewGuid(), butterId, requiredQuantity: 5, unitCostSnapshot: 3m);
        order.SetIngredientAvailability(hasShortage: false);

        var lines = order.Start(Guid.NewGuid());

        order.Status.ShouldBe(ProductionOrderStatuses.InProduction);
        order.ActualStartTime.ShouldNotBeNull();
        lines.Count.ShouldBe(2);
        lines.Single(x => x.IngredientProductId == flourId).Quantity.ShouldBe(10);
        lines.Single(x => x.IngredientProductId == butterId).TotalCost.ShouldBe(15m);
        order.Ingredients.Sum(x => x.ConsumedQuantity).ShouldBe(15);
        order.ActualIngredientCost.ShouldBe(35m);
        order.TotalProductionCost.ShouldBe(45m);
        order.UnitProductionCost.ShouldBe(2.25m);
    }

    [Fact]
    public void Start_rejects_waiting_for_ingredients_order()
    {
        var order = NewOrder();
        order.AddIngredientSnapshot(Guid.NewGuid(), Guid.NewGuid(), 10, 2m);
        order.SetIngredientAvailability(hasShortage: true);

        Should.Throw<BusinessException>(() => order.Start(Guid.NewGuid()))
            .Code.ShouldBe(ProductionErrorCodes.InvalidOrderStatusTransition);
    }

    [Fact]
    public void Complete_rejects_invalid_output_totals()
    {
        var order = ReadyStartedOrder();

        Should.Throw<BusinessException>(() =>
                order.Complete(
                    actualOutputQuantity: 20,
                    acceptedQuantity: 19,
                    rejectedQuantity: 0,
                    expiryDate: DateTime.Today.AddDays(2),
                    wasteReason: null,
                    completedByUserId: Guid.NewGuid(),
                    notes: null))
            .Code.ShouldBe(ProductionErrorCodes.InvalidOrderQuantity);
    }

    [Fact]
    public void Complete_requires_accepted_quantity()
    {
        var order = ReadyStartedOrder();

        Should.Throw<BusinessException>(() =>
                order.Complete(
                    actualOutputQuantity: 10,
                    acceptedQuantity: 0,
                    rejectedQuantity: 10,
                    expiryDate: DateTime.Today.AddDays(2),
                    wasteReason: "burnt",
                    completedByUserId: Guid.NewGuid(),
                    notes: null))
            .Code.ShouldBe(ProductionErrorCodes.CannotCompleteWithoutAcceptedQuantity);
    }

    [Fact]
    public void Complete_requires_waste_reason_when_rejected_quantity_exists()
    {
        var order = ReadyStartedOrder();

        Should.Throw<BusinessException>(() =>
                order.Complete(
                    actualOutputQuantity: 20,
                    acceptedQuantity: 18,
                    rejectedQuantity: 2,
                    expiryDate: DateTime.Today.AddDays(2),
                    wasteReason: " ",
                    completedByUserId: Guid.NewGuid(),
                    notes: null))
            .Code.ShouldBe(ProductionErrorCodes.WasteReasonRequired);
    }

    [Fact]
    public void Complete_records_output_and_actual_unit_cost()
    {
        var order = ReadyStartedOrder();
        var expiryDate = DateTime.Today.AddDays(2);

        var output = order.Complete(
            actualOutputQuantity: 20,
            acceptedQuantity: 18,
            rejectedQuantity: 2,
            expiryDate: expiryDate,
            wasteReason: " edge trim ",
            completedByUserId: Guid.NewGuid(),
            notes: "done");

        order.Status.ShouldBe(ProductionOrderStatuses.Completed);
        order.CompletedAt.ShouldNotBeNull();
        order.AcceptedQuantity.ShouldBe(18);
        order.RejectedQuantity.ShouldBe(2);
        order.WasteReason.ShouldBe("edge trim");
        order.ExpiryDate.ShouldBe(expiryDate.Date);
        order.UnitProductionCost.ShouldBe(order.TotalProductionCost / 18m);
        output.AcceptedQuantity.ShouldBe(18);
        output.UnitCost.ShouldBe(order.UnitProductionCost);
        output.ExpiryDate.ShouldBe(expiryDate.Date);
    }

    [Fact]
    public void Cancel_is_allowed_before_start_but_not_after_start()
    {
        var waiting = NewOrder();
        waiting.AddIngredientSnapshot(Guid.NewGuid(), Guid.NewGuid(), 10, 2m);
        waiting.SetIngredientAvailability(hasShortage: true);
        waiting.Cancel();
        waiting.Status.ShouldBe(ProductionOrderStatuses.Cancelled);

        var started = ReadyStartedOrder();
        Should.Throw<BusinessException>(() => started.Cancel())
            .Code.ShouldBe(ProductionErrorCodes.InvalidOrderStatusTransition);
    }

    private static AppProductionOrder ReadyStartedOrder()
    {
        var order = NewOrder();
        order.AddIngredientSnapshot(Guid.NewGuid(), Guid.NewGuid(), 10, 2m);
        order.SetIngredientAvailability(hasShortage: false);
        order.Start(Guid.NewGuid());
        return order;
    }
}
