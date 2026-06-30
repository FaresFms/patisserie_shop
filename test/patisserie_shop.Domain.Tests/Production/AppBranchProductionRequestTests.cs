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
}
