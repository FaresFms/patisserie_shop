using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Operations.Events;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Operations.Entities;

public class AppStockTransfer : FullAuditedAggregateRoot<Guid>
{
    public Guid FromBranchId { get; private set; }
    public Guid ToBranchId { get; private set; }
    public string Status { get; private set; } = null!;
    public DateTime RequestedDate { get; private set; }
    public DateTime? ApprovedDate { get; private set; }
    public DateTime? CompletedDate { get; private set; }
    public Guid? RequestedByUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<AppStockTransferItem> _items = new();
    public IReadOnlyCollection<AppStockTransferItem> Items => new ReadOnlyCollection<AppStockTransferItem>(_items);

    public bool IsDraft => Status == StockTransferStatuses.Draft;
    public bool IsTerminal => StockTransferStatuses.IsTerminal(Status);

    protected AppStockTransfer() { }

    public AppStockTransfer(
        Guid id,
        Guid fromBranchId,
        Guid toBranchId,
        DateTime requestedDate,
        Guid? requestedByUserId = null,
        string? notes = null)
        : base(id)
    {
        if (fromBranchId == toBranchId)
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
        Status = StockTransferStatuses.Pending;
    }

    public void Approve(Guid? approvedByUserId)
    {
        if (Status != StockTransferStatuses.Pending)
            throw InvalidTransition(StockTransferStatuses.Approved);

        Status = StockTransferStatuses.Approved;
        ApprovedDate = DateTime.UtcNow;
        ApprovedByUserId = approvedByUserId;

        // Default each line's approved quantity to its requested quantity so the
        // completion step always has a value to work from; admin may still adjust.
        foreach (var item in _items)
        {
            if (item.ApprovedQuantity == null)
            {
                item.SetApprovedQuantity(item.RequestedQuantity);
            }
        }
    }

    /// <summary>Admin adjusts an approved line quantity. Only valid while Approved.</summary>
    public void ApproveItem(Guid itemId, int approvedQty)
    {
        if (Status != StockTransferStatuses.Approved)
            throw new BusinessException(OperationsErrorCodes.CannotApproveTransferItem)
                .WithData("CurrentStatus", Status);
        var item = FindItem(itemId);
        item.SetApprovedQuantity(approvedQty);
    }

    public void Ship()
    {
        if (Status != StockTransferStatuses.Approved)
            throw InvalidTransition(StockTransferStatuses.InTransit);
        Status = StockTransferStatuses.InTransit;
    }

    public void Cancel()
    {
        if (Status == StockTransferStatuses.InTransit || Status == StockTransferStatuses.Completed)
            throw InvalidTransition(StockTransferStatuses.Cancelled);
        if (Status == StockTransferStatuses.Cancelled)
            return;
        Status = StockTransferStatuses.Cancelled;
    }

    /// <summary>
    /// Marks the transfer completed (InTransit → Completed). Records the actual transferred
    /// quantity per item (falling back to approved/requested when not supplied), raises
    /// TransferCompletedEto, and returns the per-item lines so the application layer can
    /// apply the matching TransferOut/TransferIn stock movements on both branches.
    /// </summary>
    public IReadOnlyList<TransferLine> Complete(IReadOnlyDictionary<Guid, int> transferredByItem)
    {
        if (Status != StockTransferStatuses.InTransit)
            throw InvalidTransition(StockTransferStatuses.Completed);

        var lines = new List<TransferLine>();
        foreach (var item in _items)
        {
            var qty = transferredByItem.TryGetValue(item.Id, out var supplied)
                ? supplied
                : (item.ApprovedQuantity ?? item.RequestedQuantity);

            item.SetTransferredQuantity(qty);
            if (qty > 0)
            {
                lines.Add(new TransferLine(item.Id, item.ProductId, qty));
            }
        }

        Status = StockTransferStatuses.Completed;
        CompletedDate = DateTime.UtcNow;

        AddDistributedEvent(new TransferCompletedEto
        {
            TransferId = Id,
            FromBranchId = FromBranchId,
            ToBranchId = ToBranchId
        });

        return lines;
    }

    // ── Helpers ──

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
