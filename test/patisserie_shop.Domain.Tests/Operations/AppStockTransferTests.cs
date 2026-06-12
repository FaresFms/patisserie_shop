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
    public void ApproveItem_Is_Only_Valid_While_Approved()
    {
        var transfer = NewTransfer();
        var item = transfer.AddItem(Guid.NewGuid(), Guid.NewGuid(), 5);

        // Draft → not allowed.
        Should.Throw<BusinessException>(() => transfer.ApproveItem(item.Id, 3))
            .Code.ShouldBe(OperationsErrorCodes.CannotApproveTransferItem);

        transfer.Submit();
        // Pending → still not allowed.
        Should.Throw<BusinessException>(() => transfer.ApproveItem(item.Id, 3))
            .Code.ShouldBe(OperationsErrorCodes.CannotApproveTransferItem);

        transfer.Approve(null);
        transfer.ApproveItem(item.Id, 3); // admin trims the line
        item.ApprovedQuantity.ShouldBe(3);
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
        eto.FromBranchId.ShouldBe(transfer.FromBranchId);
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
        pending.Cancel();
        pending.Status.ShouldBe(StockTransferStatuses.Cancelled);
        pending.Cancel(); // idempotent
        pending.Status.ShouldBe(StockTransferStatuses.Cancelled);
    }
}
