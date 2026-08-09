using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Operations.Events;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities.Auditing;

namespace Operations.Entities;

public class AppStockTransfer : FullAuditedAggregateRoot<Guid>
{
    private const string SourceDocumentTypeProperty = "Operations.SourceDocumentType";
    private const string SourceDocumentIdProperty = "Operations.SourceDocumentId";
    private const string SourceDocumentItemIdProperty = "Operations.SourceDocumentItemId";

    public Guid? FromBranchId { get; private set; }
    public Guid ToBranchId { get; private set; }
    public string Status { get; private set; } = null!;
    public DateTime RequestedDate { get; private set; }
    public DateTime? ApprovedDate { get; private set; }
    public DateTime? ShippedDate { get; private set; }
    public DateTime? CompletedDate { get; private set; }
    public Guid? RequestedByUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public Guid? ShippedByUserId { get; private set; }
    public Guid? CompletedByUserId { get; private set; }
    /// <summary>Who rejected or cancelled the transfer (terminal closures only).</summary>
    public Guid? ClosedByUserId { get; private set; }
    /// <summary>Why the transfer was rejected (required) or cancelled (optional).</summary>
    public string? ClosureReason { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<AppStockTransferItem> _items = new();
    public IReadOnlyCollection<AppStockTransferItem> Items => new ReadOnlyCollection<AppStockTransferItem>(_items);

    public bool IsDraft => Status == StockTransferStatuses.Draft;
    public bool IsTerminal => StockTransferStatuses.IsTerminal(Status);
    public string? SourceDocumentType => this.GetProperty<string>(SourceDocumentTypeProperty);
    public Guid? SourceDocumentId => this.GetProperty<Guid?>(SourceDocumentIdProperty);
    public Guid? SourceDocumentItemId => this.GetProperty<Guid?>(SourceDocumentItemIdProperty);

    protected AppStockTransfer() { }

    public AppStockTransfer(
        Guid id,
        Guid? fromBranchId,
        Guid toBranchId,
        DateTime requestedDate,
        Guid? requestedByUserId = null,
        string? notes = null)
        : base(id)
    {
        if (fromBranchId.HasValue && fromBranchId.Value == toBranchId)
            throw new BusinessException(OperationsErrorCodes.SameSourceAndDestination);

        FromBranchId = fromBranchId;
        ToBranchId = toBranchId;
        Status = StockTransferStatuses.Draft;
        RequestedDate = requestedDate;
        RequestedByUserId = requestedByUserId;
        Notes = notes;
    }

    // ── Items (Draft only) ──

    public AppStockTransferItem AddItem(Guid itemId, Guid productId, int requestedQty)
    {
        EnsureDraft();
        if (_items.Any(i => i.ProductId == productId))
        {
            throw new BusinessException(OperationsErrorCodes.DuplicateProductInTransfer)
                .WithData("ProductId", productId);
        }
        var item = new AppStockTransferItem(itemId, Id, productId, requestedQty);
        _items.Add(item);
        return item;
    }

    public void SetSourceDocument(
        string sourceDocumentType,
        Guid sourceDocumentId,
        Guid? sourceDocumentItemId = null)
    {
        EnsureDraft();
        ExtraProperties[SourceDocumentTypeProperty] = Check.NotNullOrWhiteSpace(
            sourceDocumentType,
            nameof(sourceDocumentType),
            maxLength: 128);
        ExtraProperties[SourceDocumentIdProperty] = sourceDocumentId;
        if (sourceDocumentItemId.HasValue)
        {
            ExtraProperties[SourceDocumentItemIdProperty] = sourceDocumentItemId.Value;
        }
        else
        {
            ExtraProperties.Remove(SourceDocumentItemIdProperty);
        }
    }

    public void RemoveItem(Guid itemId)
    {
        EnsureDraft();
        var item = FindItem(itemId);
        _items.Remove(item);
    }

    // ── Status machine ──

    public void Submit()
    {
        if (Status != StockTransferStatuses.Draft)
            throw InvalidTransition(StockTransferStatuses.Pending);
        if (_items.Count == 0)
            throw new BusinessException(OperationsErrorCodes.CannotSubmitEmptyTransfer);
        ChangeStatus(StockTransferStatuses.Pending);
    }

    public void Approve(Guid? approvedByUserId)
    {
        if (Status != StockTransferStatuses.Pending)
            throw InvalidTransition(StockTransferStatuses.Approved);
        if (!FromBranchId.HasValue)
            throw new BusinessException(OperationsErrorCodes.TransferSourceBranchRequired);

        ApproveCore(approvedByUserId);
    }

    public void AssignSourceAndApprove(Guid fromBranchId, Guid? approvedByUserId)
    {
        if (Status != StockTransferStatuses.Pending)
            throw InvalidTransition(StockTransferStatuses.Approved);
        if (fromBranchId == ToBranchId)
            throw new BusinessException(OperationsErrorCodes.SameSourceAndDestination);

        FromBranchId = fromBranchId;
        ApproveCore(approvedByUserId);
    }

    private void ApproveCore(Guid? approvedByUserId)
    {
        ChangeStatus(StockTransferStatuses.Approved);
        ApprovedDate = DateTime.UtcNow;
        ApprovedByUserId = approvedByUserId;

        // Default each line's approved quantity to its requested quantity so the
        // ship/receive steps always have a value to work from; approver may still adjust.
        foreach (var item in _items)
        {
            if (item.ApprovedQuantity == null)
            {
                item.SetApprovedQuantity(item.RequestedQuantity);
            }
        }
    }

    /// <summary>
    /// Approver adjusts a line quantity (up or down — e.g. rounding to tray sizes;
    /// ship-time stock validation is the backstop). Valid while the request is under
    /// review (Pending) or already Approved but not yet shipped.
    /// </summary>
    public void ApproveItem(Guid itemId, int approvedQty)
    {
        if (Status != StockTransferStatuses.Pending && Status != StockTransferStatuses.Approved)
            throw new BusinessException(OperationsErrorCodes.CannotApproveTransferItem)
                .WithData("CurrentStatus", Status);
        var item = FindItem(itemId);
        item.SetApprovedQuantity(approvedQty);
    }

    /// <summary>
    /// Rejects a pending request. A reason is mandatory — the requesting branch
    /// must be able to see why their request was refused.
    /// </summary>
    public void Reject(Guid? rejectedByUserId, string reason)
    {
        if (Status != StockTransferStatuses.Pending)
            throw InvalidTransition(StockTransferStatuses.Rejected);
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessException(OperationsErrorCodes.TransferRejectionReasonRequired);

        ClosureReason = Check.Length(reason.Trim(), nameof(reason), maxLength: 512);
        ClosedByUserId = rejectedByUserId;
        ChangeStatus(StockTransferStatuses.Rejected);
    }

    /// <summary>
    /// Marks the transfer in transit (Approved → InTransit) and returns the per-item
    /// quantities leaving the source branch so the application layer can apply the
    /// matching TransferOut stock movements. Stock physically leaves the source at
    /// this moment — not at completion.
    /// <para>
    /// <paramref name="shippedByItem"/> lets the packer ship LESS than approved (the
    /// source couldn't spare the full amount); a missing entry defaults to the approved
    /// quantity, and each value is clamped to [0, approved]. The actual shipped amount is
    /// recorded per item so the receive step caps against it.
    /// </para>
    /// </summary>
    public IReadOnlyList<TransferLine> Ship(
        IReadOnlyDictionary<Guid, int>? shippedByItem = null,
        Guid? shippedByUserId = null)
    {
        if (Status != StockTransferStatuses.Approved)
            throw InvalidTransition(StockTransferStatuses.InTransit);
        if (!FromBranchId.HasValue)
            throw new BusinessException(OperationsErrorCodes.TransferSourceBranchRequired);

        var lines = new List<TransferLine>();
        foreach (var item in _items)
        {
            var approved = item.ApprovedQuantity ?? item.RequestedQuantity;
            var qty = shippedByItem != null && shippedByItem.TryGetValue(item.Id, out var supplied)
                ? Math.Clamp(supplied, 0, approved)
                : approved;

            item.SetShippedQuantity(qty);
            if (qty > 0)
            {
                lines.Add(new TransferLine(item.Id, item.ProductId, qty));
            }
        }

        if (lines.Count == 0)
            throw new BusinessException(OperationsErrorCodes.CannotShipNothing);

        ShippedDate = DateTime.UtcNow;
        ShippedByUserId = shippedByUserId;
        ChangeStatus(StockTransferStatuses.InTransit);

        return lines;
    }

    /// <summary>
    /// Records which expiry batches actually left the source for a shipped item.
    /// Only valid right after <see cref="Ship"/> within the same unit of work.
    /// </summary>
    public void RecordItemShippedBatches(Guid itemId, string? batchBreakdown)
    {
        if (Status != StockTransferStatuses.InTransit)
            throw InvalidTransition(StockTransferStatuses.InTransit);
        FindItem(itemId).SetShippedBatchBreakdown(batchBreakdown);
    }

    public void Cancel(Guid? cancelledByUserId = null, string? reason = null)
    {
        if (Status == StockTransferStatuses.InTransit
            || Status == StockTransferStatuses.Completed
            || Status == StockTransferStatuses.Rejected)
            throw InvalidTransition(StockTransferStatuses.Cancelled);
        if (Status == StockTransferStatuses.Cancelled)
            return;

        ClosureReason = string.IsNullOrWhiteSpace(reason)
            ? null
            : Check.Length(reason.Trim(), nameof(reason), maxLength: 512);
        ClosedByUserId = cancelledByUserId;
        ChangeStatus(StockTransferStatuses.Cancelled);
    }

    /// <summary>
    /// Marks the transfer completed (InTransit → Completed). Records the actual received
    /// quantity per item — capped at what was actually SHIPPED — raises
    /// TransferCompletedEto, and returns the per-item lines so the application layer can
    /// apply the matching TransferIn stock movements at the destination. The source was
    /// already decremented at ship time; anything received short of shipped is transit
    /// loss and is simply never added back. Receiving 0 on every line closes a delivery
    /// that never arrived (see the "report lost" path in the app service).
    /// </summary>
    public IReadOnlyList<TransferLine> Complete(
        IReadOnlyDictionary<Guid, int> transferredByItem,
        Guid? completedByUserId = null)
    {
        if (Status != StockTransferStatuses.InTransit)
            throw InvalidTransition(StockTransferStatuses.Completed);
        if (!FromBranchId.HasValue)
            throw new BusinessException(OperationsErrorCodes.TransferSourceBranchRequired);

        var lines = new List<TransferLine>();
        foreach (var item in _items)
        {
            // Cap against what shipped (falls back to approved for legacy transfers
            // that predate ShippedQuantity).
            var shippedQty = item.ShippedQuantity ?? item.ApprovedQuantity ?? item.RequestedQuantity;
            var qty = transferredByItem.TryGetValue(item.Id, out var supplied)
                ? supplied
                : shippedQty;

            if (qty > shippedQty)
            {
                throw new BusinessException(OperationsErrorCodes.TransferReceivedExceedsShipped)
                    .WithData("ProductId", item.ProductId)
                    .WithData("Received", qty)
                    .WithData("Shipped", shippedQty);
            }

            item.SetTransferredQuantity(qty);
            if (qty > 0)
            {
                lines.Add(new TransferLine(item.Id, item.ProductId, qty));
            }
        }

        CompletedDate = DateTime.UtcNow;
        CompletedByUserId = completedByUserId;
        ChangeStatus(StockTransferStatuses.Completed);

        AddDistributedEvent(new TransferCompletedEto
        {
            TransferId = Id,
            FromBranchId = FromBranchId.Value,
            ToBranchId = ToBranchId,
            SourceDocumentType = SourceDocumentType,
            SourceDocumentId = SourceDocumentId,
            SourceDocumentItemId = SourceDocumentItemId,
            Lines = _items.Select(item => new TransferCompletedLineEto
            {
                TransferItemId = item.Id,
                ProductId = item.ProductId,
                ShippedQuantity = item.ShippedQuantity
                    ?? item.ApprovedQuantity
                    ?? item.RequestedQuantity,
                ReceivedQuantity = item.TransferredQuantity ?? 0
            }).ToList()
        });

        return lines;
    }

    // ── Helpers ──

    private void ChangeStatus(string newStatus)
    {
        var oldStatus = Status;
        Status = newStatus;

        AddDistributedEvent(new TransferStatusChangedEto
        {
            TransferId = Id,
            FromBranchId = FromBranchId,
            ToBranchId = ToBranchId,
            OldStatus = oldStatus,
            NewStatus = newStatus
        });
    }

    private void EnsureDraft()
    {
        if (Status != StockTransferStatuses.Draft)
            throw new BusinessException(OperationsErrorCodes.CannotModifyTransferAfterDraft)
                .WithData("CurrentStatus", Status);
    }

    private AppStockTransferItem FindItem(Guid itemId)
    {
        return _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new BusinessException(OperationsErrorCodes.TransferItemNotFound)
                .WithData("ItemId", itemId);
    }

    private BusinessException InvalidTransition(string targetStatus) =>
        new BusinessException(OperationsErrorCodes.TransferInvalidStatusTransition)
            .WithData("CurrentStatus", Status)
            .WithData("TargetStatus", targetStatus);

    public record TransferLine(Guid ItemId, Guid ProductId, int Quantity);
}
