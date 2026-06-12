using System;
using System.Linq;
using Operations;
using Operations.Entities;
using Operations.Events;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace patisserie_shop.Operations;

/// <summary>
/// Pure unit tests for the AppPurchaseOrder status machine
/// (Draft → Submitted → Approved → PartialReceived/Received, Cancel rules)
/// and the Draft-only editing guard.
/// </summary>
public class AppPurchaseOrderTests
{
    private static AppPurchaseOrder NewOrder() => new(
        Guid.NewGuid(),
        supplierId: Guid.NewGuid(),
        destBranchId: Guid.NewGuid(),
        poNumber: "PO-2026-0001",
        orderDate: new DateTime(2026, 6, 1));

    private static AppPurchaseOrderItem AddLine(AppPurchaseOrder po, int qty = 10, decimal price = 2m)
        => po.AddItem(Guid.NewGuid(), Guid.NewGuid(), qty, price);

    [Fact]
    public void New_Order_Is_Draft_And_Item_Edits_Recalculate_Total()
    {
        var po = NewOrder();
        po.Status.ShouldBe(PurchaseOrderStatuses.Draft);
        po.TotalAmount.ShouldBe(0m);

        var a = AddLine(po, qty: 10, price: 2m);   // 20
        var b = AddLine(po, qty: 5, price: 4m);    // 20
        po.TotalAmount.ShouldBe(40m);

        po.UpdateItem(a.Id, orderedQty: 20, unitPrice: 2m); // 40 + 20
        po.TotalAmount.ShouldBe(60m);

        po.RemoveItem(b.Id);
        po.TotalAmount.ShouldBe(40m);
    }

    [Fact]
    public void AddItem_Should_Reject_Duplicate_Product()
    {
        var po = NewOrder();
        var productId = Guid.NewGuid();
        po.AddItem(Guid.NewGuid(), productId, 1, 1m);

        Should.Throw<BusinessException>(() => po.AddItem(Guid.NewGuid(), productId, 2, 1m))
            .Code.ShouldBe(OperationsErrorCodes.DuplicateProductInOrder);
    }

    [Fact]
    public void Submit_Should_Reject_Empty_Order_And_Approve_Requires_Submitted()
    {
        var po = NewOrder();

        Should.Throw<BusinessException>(() => po.Submit())
            .Code.ShouldBe(OperationsErrorCodes.CannotSubmitEmptyOrder);

        // Approve straight from Draft is an invalid transition.
        Should.Throw<BusinessException>(() => po.Approve())
            .Code.ShouldBe(OperationsErrorCodes.InvalidStatusTransition);

        AddLine(po);
        po.Submit();
        po.Status.ShouldBe(PurchaseOrderStatuses.Submitted);
        po.Approve();
        po.Status.ShouldBe(PurchaseOrderStatuses.Approved);
    }

    [Fact]
    public void Editing_Items_Or_Header_Is_Blocked_After_Draft()
    {
        var po = NewOrder();
        AddLine(po);
        po.Submit();

        Should.Throw<BusinessException>(() => AddLine(po))
            .Code.ShouldBe(OperationsErrorCodes.CannotModifyAfterDraft);
        Should.Throw<BusinessException>(() =>
                po.EditHeader(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, null, "edited"))
            .Code.ShouldBe(OperationsErrorCodes.CannotModifyAfterDraft);
    }

    [Fact]
    public void Receipt_Walks_Partial_Then_Received_And_Publishes_PurchaseReceivedEto()
    {
        var po = NewOrder();
        var item = AddLine(po, qty: 10, price: 2m);
        po.Submit();
        po.Approve();

        // Partial receipt: 4 of 10.
        var firstDelta = po.RecordReceipt(new[] { (item.Id, 4) });
        firstDelta.ShouldHaveSingleItem().Delta.ShouldBe(4);
        po.Status.ShouldBe(PurchaseOrderStatuses.PartialReceived);
        po.ActualDeliveryDate.ShouldBeNull();

        // Remainder: 6 of 10 → fully received.
        po.RecordReceipt(new[] { (item.Id, 6) });
        po.Status.ShouldBe(PurchaseOrderStatuses.Received);
        po.ActualDeliveryDate.ShouldNotBeNull();
        item.IsFullyReceived.ShouldBeTrue();

        var eto = po.GetDistributedEvents()
            .Select(e => e.EventData)
            .OfType<PurchaseReceivedEto>()
            .ShouldHaveSingleItem();
        eto.PurchaseOrderId.ShouldBe(po.Id);
        eto.DestBranchId.ShouldBe(po.DestBranchId);
    }

    [Fact]
    public void Receipt_Should_Reject_Overdelivery()
    {
        var po = NewOrder();
        var item = AddLine(po, qty: 10);
        po.Submit();
        po.Approve();
        po.RecordReceipt(new[] { (item.Id, 8) });

        // 8 already received; 3 more would exceed the 10 ordered.
        Should.Throw<BusinessException>(() => po.RecordReceipt(new[] { (item.Id, 3) }))
            .Code.ShouldBe(OperationsErrorCodes.ReceivedExceedsOrdered);
    }

    [Fact]
    public void Receipt_Requires_A_Receivable_Status()
    {
        var po = NewOrder();
        var item = AddLine(po);

        // Draft is not receivable.
        Should.Throw<BusinessException>(() => po.RecordReceipt(new[] { (item.Id, 1) }))
            .Code.ShouldBe(OperationsErrorCodes.InvalidStatusTransition);
    }

    [Fact]
    public void Cancel_Is_Blocked_After_Full_Receipt_But_Allowed_From_Draft()
    {
        var received = NewOrder();
        var item = AddLine(received, qty: 1);
        received.Submit();
        received.Approve();
        received.RecordReceipt(new[] { (item.Id, 1) });
        Should.Throw<BusinessException>(() => received.Cancel())
            .Code.ShouldBe(OperationsErrorCodes.InvalidStatusTransition);

        var draft = NewOrder();
        draft.Cancel();
        draft.Status.ShouldBe(PurchaseOrderStatuses.Cancelled);
        draft.Cancel(); // idempotent — no throw
        draft.Status.ShouldBe(PurchaseOrderStatuses.Cancelled);
    }
}
