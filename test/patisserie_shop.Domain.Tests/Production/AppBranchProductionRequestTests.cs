using System;
using System.Collections.Generic;
using Production;
using Production.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace patisserie_shop.Production;

public class AppBranchProductionRequestTests
{
    private static AppBranchProductionRequest NewRequest()
    {
        return new AppBranchProductionRequest(
            Guid.NewGuid(),
            "REQ-TEST",
            Guid.NewGuid(),
            DateTime.Today.AddDays(1),
            ProductionPriorities.Normal,
            Guid.NewGuid(),
            "n");
    }

    [Fact]
    public void Submit_requires_items()
    {
        var request = NewRequest();

        Should.Throw<BusinessException>(() => request.Submit())
            .Code.ShouldBe(ProductionErrorCodes.CannotSubmitEmptyRequest);
    }

    [Fact]
    public void Approve_requires_reason_when_quantity_is_adjusted()
    {
        var request = NewRequest();
        var item = request.AddItem(Guid.NewGuid(), Guid.NewGuid(), 80);
        request.Submit();

        Should.Throw<BusinessException>(() => request.Approve(
                Guid.NewGuid(),
                new Dictionary<Guid, int> { [item.Id] = 70 },
                reason: null))
            .Code.ShouldBe(ProductionErrorCodes.ApprovalAdjustmentReasonRequired);
    }

    [Fact]
    public void Approve_sets_approved_quantities()
    {
        var request = NewRequest();
        var item = request.AddItem(Guid.NewGuid(), Guid.NewGuid(), 80);
        request.Submit();

        request.Approve(Guid.NewGuid(), new Dictionary<Guid, int> { [item.Id] = 70 }, "butter shortage");

        request.Status.ShouldBe(BranchProductionRequestStatuses.Approved);
        item.ApprovedQuantity.ShouldBe(70);
        request.DecisionReason.ShouldBe("butter shortage");
    }

    [Fact]
    public void Duplicate_product_is_rejected()
    {
        var request = NewRequest();
        var productId = Guid.NewGuid();

        request.AddItem(Guid.NewGuid(), productId, 10);

        Should.Throw<BusinessException>(() => request.AddItem(Guid.NewGuid(), productId, 5))
            .Code.ShouldBe(ProductionErrorCodes.DuplicateRequestProduct);
    }

    [Fact]
    public void Request_reaches_Fulfilled_even_when_a_line_was_approved_at_zero()
    {
        // One product fully approved, one rejected at the line level (approved 0).
        var request = NewRequest();
        var kept = request.AddItem(Guid.NewGuid(), Guid.NewGuid(), 20);
        var rejectedLine = request.AddItem(Guid.NewGuid(), Guid.NewGuid(), 15);
        request.Submit();
        request.Approve(
            Guid.NewGuid(),
            new Dictionary<Guid, int> { [kept.Id] = 20, [rejectedLine.Id] = 0 },
            "no cream available for the second item");

        // Deliver the whole approved quantity of the kept line. The 0-approved line
        // needs nothing, so the request must complete rather than stay stuck.
        request.AddFulfilledQuantity(kept.Id, 20);

        request.Status.ShouldBe(BranchProductionRequestStatuses.Fulfilled);
    }

    [Fact]
    public void Request_is_partially_fulfilled_until_all_approved_quantities_are_delivered()
    {
        var request = NewRequest();
        var a = request.AddItem(Guid.NewGuid(), Guid.NewGuid(), 20);
        var b = request.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10);
        request.Submit();
        request.Approve(Guid.NewGuid(), new Dictionary<Guid, int> { [a.Id] = 20, [b.Id] = 10 }, null);

        request.AddFulfilledQuantity(a.Id, 20);

        request.Status.ShouldBe(BranchProductionRequestStatuses.PartiallyFulfilled);
    }

    [Fact]
    public void Planning_reserves_only_the_approved_quantity_and_release_makes_it_available_again()
    {
        var request = NewRequest();
        var item = request.AddItem(Guid.NewGuid(), Guid.NewGuid(), 20);
        request.Submit();
        request.Approve(Guid.NewGuid(), new Dictionary<Guid, int> { [item.Id] = 20 }, null);

        request.ReservePlannedQuantity(item.Id, 12);
        request.Status.ShouldBe(BranchProductionRequestStatuses.PartiallyPlanned);
        item.PlannedQuantity.ShouldBe(12);

        request.ReservePlannedQuantity(item.Id, 8);
        request.Status.ShouldBe(BranchProductionRequestStatuses.Planned);
        Should.Throw<BusinessException>(() => request.ReservePlannedQuantity(item.Id, 1))
            .Code.ShouldBe(ProductionErrorCodes.RequestPlannedQuantityExceeded);

        request.ReleasePlannedQuantity(item.Id, 5);
        request.Status.ShouldBe(BranchProductionRequestStatuses.PartiallyPlanned);
        item.PlannedQuantity.ShouldBe(15);
    }

    [Fact]
    public void Shipping_does_not_fulfill_a_request_but_receipt_does()
    {
        var request = NewRequest();
        var item = request.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10);
        request.Submit();
        request.Approve(Guid.NewGuid(), new Dictionary<Guid, int> { [item.Id] = 10 }, null);
        request.ReservePlannedQuantity(item.Id, 10);

        request.Status.ShouldBe(BranchProductionRequestStatuses.Planned);
        item.FulfilledQuantity.ShouldBe(0);

        request.AddFulfilledQuantity(item.Id, 7);
        request.Status.ShouldBe(BranchProductionRequestStatuses.PartiallyFulfilled);
        item.FulfilledQuantity.ShouldBe(7);
    }
}
