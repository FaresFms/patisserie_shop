using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Operations.Events;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Operations.Entities;

public class AppPurchaseOrder : FullAuditedAggregateRoot<Guid>
{
    public Guid SupplierId { get; private set; }
    public Guid DestBranchId { get; private set; }
    public string PONumber { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public DateTime OrderDate { get; private set; }
    public DateTime? ExpectedDeliveryDate { get; private set; }
    public DateTime? ActualDeliveryDate { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string? Notes { get; private set; }

    private readonly List<AppPurchaseOrderItem> _items = new();
    public IReadOnlyCollection<AppPurchaseOrderItem> Items => new ReadOnlyCollection<AppPurchaseOrderItem>(_items);

    public bool IsDraft => Status == PurchaseOrderStatuses.Draft;
    public bool IsReceivable => PurchaseOrderStatuses.IsReceivable(Status);
    public bool IsTerminal => PurchaseOrderStatuses.IsTerminal(Status);

    protected AppPurchaseOrder() { }

    public AppPurchaseOrder(
        Guid id,
        Guid supplierId,
        Guid destBranchId,
        string poNumber,
        DateTime orderDate,
        DateTime? expectedDeliveryDate = null,
        string currency = "USD",
        string? notes = null)
        : base(id)
    {
        SupplierId = supplierId;
        DestBranchId = destBranchId;
        PONumber = Check.NotNullOrWhiteSpace(poNumber, nameof(poNumber));
        Status = PurchaseOrderStatuses.Draft;
        OrderDate = orderDate;
        ExpectedDeliveryDate = expectedDeliveryDate;
        Currency = currency;
        Notes = notes;
        TotalAmount = 0m;
    }

    // ── Header editing (Draft only) ──

    public void EditHeader(Guid supplierId, Guid destBranchId, DateTime orderDate, DateTime? expectedDeliveryDate, string? notes)
    {
        EnsureDraft();
        SupplierId = supplierId;
        DestBranchId = destBranchId;
        OrderDate = orderDate;
        ExpectedDeliveryDate = expectedDeliveryDate;
        Notes = notes;
    }

    // ── Items (Draft only) ──

    public AppPurchaseOrderItem AddItem(Guid itemId, Guid productId, int orderedQty, decimal unitPrice)
    {
        EnsureDraft();
        if (_items.Any(i => i.ProductId == productId))
        {
            throw new BusinessException(OperationsErrorCodes.DuplicateProductInOrder)
                .WithData("ProductId", productId);
        }
        var item = new AppPurchaseOrderItem(itemId, Id, productId, orderedQty, unitPrice);
        _items.Add(item);
        RecalculateTotal();
        return item;
    }

    public void UpdateItem(Guid itemId, int orderedQty, decimal unitPrice)
    {
        EnsureDraft();
        var item = FindItem(itemId);
        item.ChangeOrderedQuantity(orderedQty);
        item.ChangeUnitPrice(unitPrice);
        RecalculateTotal();
    }

    public void RemoveItem(Guid itemId)
    {
        EnsureDraft();
        var item = FindItem(itemId);
        _items.Remove(item);
        RecalculateTotal();
    }

    // ── Status machine ──

    public void Submit()
    {
        if (Status != PurchaseOrderStatuses.Draft)
            throw InvalidTransition(PurchaseOrderStatuses.Submitted);
        if (_items.Count == 0)
            throw new BusinessException(OperationsErrorCodes.CannotSubmitEmptyOrder);
        Status = PurchaseOrderStatuses.Submitted;
    }

    public void Approve()
    {
        if (Status != PurchaseOrderStatuses.Submitted)
            throw InvalidTransition(PurchaseOrderStatuses.Approved);
        Status = PurchaseOrderStatuses.Approved;
    }

    public void Cancel()
    {
        if (Status == PurchaseOrderStatuses.Received)
            throw InvalidTransition(PurchaseOrderStatuses.Cancelled);
        if (Status == PurchaseOrderStatuses.Cancelled)
            return;
        Status = PurchaseOrderStatuses.Cancelled;
    }

    /// <summary>
    /// Records partial or full receipt. Returns the per-item received delta so the caller
    /// (application layer) can apply matching stock adjustments via BranchInventoryManager.
    /// Raises PurchaseReceivedEto when the order becomes fully received.
    /// </summary>
    public IReadOnlyList<ReceivedLine> RecordReceipt(IEnumerable<(Guid ItemId, int ReceivedQty)> receipts)
    {
        if (!IsReceivable)
            throw InvalidTransition(PurchaseOrderStatuses.Received);

        var applied = new List<ReceivedLine>();
        foreach (var (itemId, qty) in receipts)
        {
            if (qty <= 0) continue;
            var item = FindItem(itemId);
            var delta = item.Receive(qty);
            if (delta > 0)
            {
                applied.Add(new ReceivedLine(item.Id, item.ProductId, delta, item.UnitPrice));
            }
        }

        if (_items.All(i => i.IsFullyReceived))
        {
            Status = PurchaseOrderStatuses.Received;
            ActualDeliveryDate = DateTime.UtcNow;
            AddDistributedEvent(new PurchaseReceivedEto
            {
                PurchaseOrderId = Id,
                SupplierId = SupplierId,
                DestBranchId = DestBranchId
            });
        }
        else if (_items.Any(i => i.ReceivedQuantity > 0))
        {
            Status = PurchaseOrderStatuses.PartialReceived;
        }

        return applied;
    }

    // ── Helpers ──

    private void EnsureDraft()
    {
        if (Status != PurchaseOrderStatuses.Draft)
            throw new BusinessException(OperationsErrorCodes.CannotModifyAfterDraft)
                .WithData("CurrentStatus", Status);
    }

    private AppPurchaseOrderItem FindItem(Guid itemId)
    {
        return _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new BusinessException(OperationsErrorCodes.PurchaseOrderItemNotFound)
                .WithData("ItemId", itemId);
    }

    private void RecalculateTotal() => TotalAmount = _items.Sum(i => i.Subtotal);

    private BusinessException InvalidTransition(string targetStatus) =>
        new BusinessException(OperationsErrorCodes.InvalidStatusTransition)
            .WithData("CurrentStatus", Status)
            .WithData("TargetStatus", targetStatus);

    public record ReceivedLine(Guid ItemId, Guid ProductId, int Delta, decimal UnitPrice);
}
