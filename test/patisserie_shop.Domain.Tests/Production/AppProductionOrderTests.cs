using System;
using System.Linq;
using Production;
using Production.Entities;
using Production.Orders;
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
    public void Start_uses_the_latest_pre_consumption_ingredient_cost_snapshot()
    {
        var order = NewOrder();
        var ingredientId = Guid.NewGuid();
        order.AddIngredientSnapshot(Guid.NewGuid(), ingredientId, requiredQuantity: 10, unitCostSnapshot: 2m);
        order.SetIngredientAvailability(hasShortage: false);

        order.RefreshIngredientCostSnapshots(
            new System.Collections.Generic.Dictionary<Guid, decimal> { [ingredientId] = 3m });
        order.Start(Guid.NewGuid());

        order.ActualIngredientCost.ShouldBe(30m);
        order.Ingredients.Single().UnitCostSnapshot.ShouldBe(3m);
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
    public void Complete_allows_a_fully_rejected_batch_without_creating_output()
    {
        var order = ReadyStartedOrder();

        var completion = order.Complete(
            actualOutputQuantity: 10,
            acceptedQuantity: 0,
            rejectedQuantity: 10,
            expiryDate: null,
            wasteReason: "burnt",
            completedByUserId: Guid.NewGuid(),
            notes: null);

        order.Status.ShouldBe(ProductionOrderStatuses.Completed);
        order.AcceptedQuantity.ShouldBe(0);
        order.RejectedQuantity.ShouldBe(10);
        order.ExpiryDate.ShouldBeNull();
        order.UnitProductionCost.ShouldBe(0m);
        completion.Output.AcceptedQuantity.ShouldBe(0);
        completion.Output.ExpiryDate.ShouldBeNull();
    }

    [Fact]
    public void Expiry_policy_defaults_to_shelf_life_and_allows_an_earlier_date()
    {
        var productId = Guid.NewGuid();
        var productionDate = new DateTime(2026, 8, 4);

        ProductionExpiryPolicy.Resolve(null, 3, productionDate, productId)
            .ShouldBe(new DateTime(2026, 8, 7));
        ProductionExpiryPolicy.Resolve(new DateTime(2026, 8, 6), 3, productionDate, productId)
            .ShouldBe(new DateTime(2026, 8, 6));
    }

    [Fact]
    public void Expiry_policy_rejects_a_date_beyond_product_shelf_life()
    {
        var exception = Should.Throw<BusinessException>(() =>
            ProductionExpiryPolicy.Resolve(
                new DateTime(2026, 8, 8),
                shelfLifeDays: 3,
                productionDate: new DateTime(2026, 8, 4),
                productId: Guid.NewGuid()));

        exception.Code.ShouldBe(ProductionErrorCodes.ProductionExpiryDateExceedsShelfLife);
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
    public void Complete_rejects_expiry_before_the_cook_started()
    {
        var order = ReadyStartedOrder();

        Should.Throw<BusinessException>(() => order.Complete(
                actualOutputQuantity: 20,
                acceptedQuantity: 20,
                rejectedQuantity: 0,
                expiryDate: order.ActualStartTime!.Value.Date.AddDays(-1),
                wasteReason: null,
                completedByUserId: Guid.NewGuid(),
                notes: null))
            .Code.ShouldBe(ProductionErrorCodes.ProductionExpiryDateInPast);
    }

    [Fact]
    public void Complete_records_output_and_actual_unit_cost()
    {
        var order = ReadyStartedOrder();
        var expiryDate = DateTime.Today.AddDays(2);

        var completion = order.Complete(
            actualOutputQuantity: 20,
            acceptedQuantity: 18,
            rejectedQuantity: 2,
            expiryDate: expiryDate,
            wasteReason: " edge trim ",
            completedByUserId: Guid.NewGuid(),
            notes: "done");
        var output = completion.Output;

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
    public void Dispatch_tracks_remaining_in_transit_received_and_loss_across_multiple_transfers()
    {
        var order = NewOrder();
        var requestId = Guid.NewGuid();
        var requestItemId = Guid.NewGuid();
        var destinationBranchId = Guid.NewGuid();
        order.AddAllocation(
            Guid.NewGuid(),
            destinationBranchId,
            requestId,
            requestItemId,
            allocatedQuantity: 12);
        order.AddIngredientSnapshot(Guid.NewGuid(), Guid.NewGuid(), 10, 2m);
        order.SetIngredientAvailability(hasShortage: false);
        order.Start(Guid.NewGuid());
        order.Complete(12, 12, 0, DateTime.Today.AddDays(2), null, Guid.NewGuid(), null);
        order.ReleaseQuality("passed", Guid.NewGuid(), DateTime.UtcNow);

        var firstTransferId = Guid.NewGuid();
        order.CreateDispatch(
            Guid.NewGuid(), firstTransferId, destinationBranchId, 5,
            DateTime.UtcNow, Guid.NewGuid);

        order.RemainingToDispatch.ShouldBe(7);
        order.InTransitQuantity.ShouldBe(5);
        Should.Throw<BusinessException>(() => order.CreateDispatch(
                Guid.NewGuid(), Guid.NewGuid(), destinationBranchId, 8,
                DateTime.UtcNow, Guid.NewGuid))
            .Code.ShouldBe(ProductionErrorCodes.DispatchQuantityExceedsRemaining);

        var firstReceipt = order.CompleteDispatch(firstTransferId, 4, DateTime.UtcNow);
        firstReceipt.Sum(x => x.ReceivedQuantity).ShouldBe(4);
        firstReceipt.Sum(x => x.LostQuantity).ShouldBe(1);
        order.ReceivedQuantity.ShouldBe(4);
        order.LostQuantity.ShouldBe(1);
        order.InTransitQuantity.ShouldBe(0);

        var secondTransferId = Guid.NewGuid();
        order.CreateDispatch(
            Guid.NewGuid(), secondTransferId, destinationBranchId, 7,
            DateTime.UtcNow, Guid.NewGuid);
        order.CompleteDispatch(secondTransferId, 7, DateTime.UtcNow);

        order.DispatchedQuantity.ShouldBe(12);
        order.ReceivedQuantity.ShouldBe(11);
        order.LostQuantity.ShouldBe(1);
        order.RemainingToDispatch.ShouldBe(0);
    }

    [Fact]
    public void Completing_short_output_releases_unproduced_request_allocations()
    {
        var order = NewOrder();
        var requestId = Guid.NewGuid();
        var requestItemId = Guid.NewGuid();
        order.AddAllocation(Guid.NewGuid(), Guid.NewGuid(), requestId, requestItemId, 20);
        order.AddIngredientSnapshot(Guid.NewGuid(), Guid.NewGuid(), 10, 2m);
        order.SetIngredientAvailability(hasShortage: false);
        order.Start(Guid.NewGuid());

        var completion = order.Complete(
            20, 15, 5, DateTime.Today.AddDays(2), "damaged", Guid.NewGuid(), null);

        completion.ReleasedAllocations.Sum(x => x.Quantity).ShouldBe(5);
        order.ReservedQuantity.ShouldBe(15);
        order.RemainingToDispatch.ShouldBe(15);
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

    [Fact]
    public void Completed_output_requires_quality_release_before_dispatch()
    {
        var order = NewOrder();
        var destinationBranchId = Guid.NewGuid();
        order.AddAllocation(Guid.NewGuid(), destinationBranchId, Guid.NewGuid(), Guid.NewGuid(), 5);
        order.AddIngredientSnapshot(Guid.NewGuid(), Guid.NewGuid(), 5, 2m);
        order.SetIngredientAvailability(false);
        order.Start(Guid.NewGuid());
        order.Complete(5, 5, 0, DateTime.Today.AddDays(2), null, Guid.NewGuid(), null);

        order.QualityStatus.ShouldBe(ProductionQualityStatuses.Pending);
        Should.Throw<BusinessException>(() => order.CreateDispatch(
                Guid.NewGuid(), Guid.NewGuid(), destinationBranchId, 5, DateTime.UtcNow, Guid.NewGuid))
            .Code.ShouldBe(ProductionErrorCodes.QualityReleaseRequiredForDispatch);

        order.HoldQuality("temperature check", Guid.NewGuid(), DateTime.UtcNow);
        order.QualityStatus.ShouldBe(ProductionQualityStatuses.Held);
        order.ReleaseQuality("passed", Guid.NewGuid(), DateTime.UtcNow);
        Should.NotThrow(() => order.CreateDispatch(
            Guid.NewGuid(), Guid.NewGuid(), destinationBranchId, 5, DateTime.UtcNow, Guid.NewGuid));
    }

    [Fact]
    public void Schedule_and_operator_gates_are_enforced_before_start()
    {
        var order = NewOrder();
        var start = DateTime.UtcNow.AddHours(1);

        Should.Throw<BusinessException>(() => order.EnsureReadyForStart(true, true))
            .Code.ShouldBe(ProductionErrorCodes.ProductionScheduleRequired);

        order.Schedule("OVEN-1", "DAY", start, start.AddHours(2), Guid.NewGuid(), "Baker");
        Should.NotThrow(() => order.EnsureReadyForStart(true, true));
        order.WorkCenterCode.ShouldBe("OVEN-1");
        order.AssignedOperatorName.ShouldBe("Baker");
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
