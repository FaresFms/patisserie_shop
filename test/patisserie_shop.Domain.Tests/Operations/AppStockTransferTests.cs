using System;
using System.Collections.Generic;
using System.Linq;
using Operations;
using Operations.Entities;
using Operations.Events;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace patisserie_shop.Operations;

/// <summary>
/// Pure unit tests for the AppStockTransfer status machine
/// (Draft → Pending → Approved → InTransit → Completed) including per-item
/// approval rules, quantity fallbacks on completion, and Cancel guards.
/// </summary>
public class AppStockTransferTests
{
    private static AppStockTransfer NewTransfer() => new(
        Guid.NewGuid(),
        fromBranchId: Guid.NewGuid(),
        toBranchId: Guid.NewGuid(),
        requestedDate: new DateTime(2026, 6, 1));

    [Fact]
    public void Constructor_Should_Reject_Same_Source_And_Destination()
    {
        var branchId = Guid.NewGuid();

        Should.Throw<BusinessException>(() =>
                new AppStockTransfer(Guid.NewGuid(), branchId, branchId, DateTime.Today))
            .Code.ShouldBe(OperationsErrorCodes.SameSourceAndDestination);
    }

    [Fact]
    public void AddItem_Should_Reject_Duplicates_And_NonPositive_Quantities()
    {
        var transfer = NewTransfer();
        var productId = Guid.NewGuid();
        transfer.AddItem(Guid.NewGuid(), productId, requestedQty: 5);

        Should.Throw<BusinessException>(() => transfer.AddItem(Guid.NewGuid(), productId, 1))
            .Code.ShouldBe(OperationsErrorCodes.DuplicateProductInTransfer);
        Should.Throw<BusinessException>(() => transfer.AddItem(Guid.NewGuid(), Guid.NewGuid(), 0))
            .Code.ShouldBe(OperationsErrorCodes.TransferInvalidQuantity);
    }

    [Fact]
    public void Submit_Should_Reject_Empty_Transfer()
    {
        var transfer = NewTransfer();

        Should.Throw<BusinessException>(() => transfer.Submit())
            .Code.ShouldBe(OperationsErrorCodes.CannotSubmitEmptyTransfer);
    }

    [Fact]
    public void Items_Are_Frozen_After_Submit()
    {
        var transfer = NewTransfer();
        transfer.AddItem(Guid.NewGuid(), Guid.NewGuid(), 5);
        transfer.Submit();
        transfer.Status.ShouldBe(StockTransferStatuses.Pending);

        Should.Throw<BusinessException>(() => transfer.AddItem(Guid.NewGuid(), Guid.NewGuid(), 1))
            .Code.ShouldBe(OperationsErrorCodes.CannotModifyTransferAfterDraft);
    }

    [Fact]
    public void Approve_Should_Default_Approved_Quantities_To_Requested()
    {
        var transfer = NewTransfer();
        var item = transfer.AddItem(Guid.NewGuid(), Guid.NewGuid(), requestedQty: 7);
        transfer.Submit();

        transfer.Approve(approvedByUserId: Guid.NewGuid());

        transfer.Status.ShouldBe(StockTransferStatuses.Approved);
        transfer.ApprovedDate.ShouldNotBeNull();
        item.ApprovedQuantity.ShouldBe(7);
    }

    [Fact]
    public void ApproveItem_Is_Valid_While_Pending_Or_Approved()
    {
        var transfer = NewTransfer();
        var item = transfer.AddItem(Guid.NewGuid(), Guid.NewGuid(), 5);

        // Draft → not allowed.
        Should.Throw<BusinessException>(() => transfer.ApproveItem(item.Id, 3))
            .Code.ShouldBe(OperationsErrorCodes.CannotApproveTransferItem);

        transfer.Submit();
        // Pending → approver trims the line while reviewing, before approving.
        transfer.ApproveItem(item.Id, 4);
        item.ApprovedQuantity.ShouldBe(4);

        // Approve keeps the reviewed quantity instead of resetting to requested.
        transfer.Approve(null);
        item.ApprovedQuantity.ShouldBe(4);

        transfer.ApproveItem(item.Id, 3); // still adjustable until shipped
        item.ApprovedQuantity.ShouldBe(3);

        transfer.Ship();
        Should.Throw<BusinessException>(() => transfer.ApproveItem(item.Id, 2))
            .Code.ShouldBe(OperationsErrorCodes.CannotApproveTransferItem);
    }

    [Fact]
    public void Reject_Requires_Pending_And_A_Reason()
    {
        var transfer = NewTransfer();
        transfer.AddItem(Guid.NewGuid(), Guid.NewGuid(), 5);

        // Draft → not allowed.
        Should.Throw<BusinessException>(() => transfer.Reject(Guid.NewGuid(), "no stock"))
            .Code.ShouldBe(OperationsErrorCodes.TransferInvalidStatusTransition);

        transfer.Submit();

        Should.Throw<BusinessException>(() => transfer.Reject(Guid.NewGuid(), "  "))
            .Code.ShouldBe(OperationsErrorCodes.TransferRejectionReasonRequired);

        var rejectedBy = Guid.NewGuid();
        transfer.Reject(rejectedBy, "Not enough stock at any branch this week.");

        transfer.Status.ShouldBe(StockTransferStatuses.Rejected);
        transfer.ClosureReason.ShouldBe("Not enough stock at any branch this week.");
        transfer.ClosedByUserId.ShouldBe(rejectedBy);
        transfer.IsTerminal.ShouldBeTrue();

        // Terminal — cannot be cancelled or re-approved afterwards.
        Should.Throw<BusinessException>(() => transfer.Cancel())
            .Code.ShouldBe(OperationsErrorCodes.TransferInvalidStatusTransition);
        Should.Throw<BusinessException>(() => transfer.Approve(null))
            .Code.ShouldBe(OperationsErrorCodes.TransferInvalidStatusTransition);
    }

    [Fact]
    public void Complete_Rejects_Receiving_More_Than_Was_Shipped()
    {
        var transfer = NewTransfer();
        var item = transfer.AddItem(Guid.NewGuid(), Guid.NewGuid(), requestedQty: 10);
        transfer.Submit();
        transfer.Approve(null);
        transfer.Ship();

        Should.Throw<BusinessException>(() =>
                transfer.Complete(new Dictionary<Guid, int> { [item.Id] = 11 }))
            .Code.ShouldBe(OperationsErrorCodes.TransferReceivedExceedsShipped);
    }

    [Fact]
    public void Ship_Records_Audit_Trail_And_Returns_Shipping_Lines()
    {
        var transfer = NewTransfer();
        var item = transfer.AddItem(Guid.NewGuid(), Guid.NewGuid(), requestedQty: 6);
        transfer.Submit();
        transfer.Approve(null);
        transfer.ApproveItem(item.Id, 5);

        var shippedBy = Guid.NewGuid();
        var lines = transfer.Ship(shippedBy);

        transfer.Status.ShouldBe(StockTransferStatuses.InTransit);
        transfer.ShippedDate.ShouldNotBeNull();
        transfer.ShippedByUserId.ShouldBe(shippedBy);
        lines.ShouldHaveSingleItem().Quantity.ShouldBe(5);

        transfer.RecordItemShippedBatches(item.Id, "2026-07-03:5");
        item.ShippedBatchBreakdown.ShouldBe("2026-07-03:5");
    }

    [Fact]
    public void Ship_Requires_Approved_And_Complete_Requires_InTransit()
    {
        var transfer = NewTransfer();
        transfer.AddItem(Guid.NewGuid(), Guid.NewGuid(), 5);
        transfer.Submit();

        Should.Throw<BusinessException>(() => transfer.Ship())
            .Code.ShouldBe(OperationsErrorCodes.TransferInvalidStatusTransition);
        Should.Throw<BusinessException>(() => transfer.Complete(new Dictionary<Guid, int>()))
            .Code.ShouldBe(OperationsErrorCodes.TransferInvalidStatusTransition);
    }

    [Fact]
    public void Complete_Uses_Supplied_Quantity_Or_Falls_Back_To_Approved_And_Publishes_Eto()
    {
        var transfer = NewTransfer();
        var adjusted = transfer.AddItem(Guid.NewGuid(), Guid.NewGuid(), requestedQty: 10);
        var untouched = transfer.AddItem(Guid.NewGuid(), Guid.NewGuid(), requestedQty: 4);
        transfer.Submit();
        transfer.Approve(null);
        transfer.Ship();
        transfer.Status.ShouldBe(StockTransferStatuses.InTransit);

        // Caller reports only the first line; the second falls back to its approved qty.
        var lines = transfer.Complete(new Dictionary<Guid, int> { [adjusted.Id] = 8 });

        transfer.Status.ShouldBe(StockTransferStatuses.Completed);
        transfer.CompletedDate.ShouldNotBeNull();
        lines.Count.ShouldBe(2);
        lines.Single(l => l.ItemId == adjusted.Id).Quantity.ShouldBe(8);
        lines.Single(l => l.ItemId == untouched.Id).Quantity.ShouldBe(4);
        adjusted.TransferredQuantity.ShouldBe(8);
        untouched.TransferredQuantity.ShouldBe(4);

        var eto = transfer.GetDistributedEvents()
            .Select(e => e.EventData)
            .OfType<TransferCompletedEto>()
            .ShouldHaveSingleItem();
        eto.TransferId.ShouldBe(transfer.Id);
        eto.FromBranchId.ShouldBe(transfer.FromBranchId!.Value);
        eto.ToBranchId.ShouldBe(transfer.ToBranchId);
    }

    [Fact]
    public void Cancel_Is_Blocked_Once_Goods_Are_Moving_But_Allowed_While_Pending()
    {
        var moving = NewTransfer();
        moving.AddItem(Guid.NewGuid(), Guid.NewGuid(), 5);
        moving.Submit();
        moving.Approve(null);
        moving.Ship();
        Should.Throw<BusinessException>(() => moving.Cancel())
            .Code.ShouldBe(OperationsErrorCodes.TransferInvalidStatusTransition);

        var pending = NewTransfer();
        pending.AddItem(Guid.NewGuid(), Guid.NewGuid(), 5);
        pending.Submit();
        var cancelledBy = Guid.NewGuid();
        pending.Cancel(cancelledBy, "Ordered by mistake");
        pending.Status.ShouldBe(StockTransferStatuses.Cancelled);
        pending.ClosureReason.ShouldBe("Ordered by mistake");
        pending.ClosedByUserId.ShouldBe(cancelledBy);
        pending.Cancel(); // idempotent
        pending.Status.ShouldBe(StockTransferStatuses.Cancelled);
    }
}
