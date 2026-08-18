using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Production.Entities;

public class AppBranchProductionRequest : FullAuditedAggregateRoot<Guid>
{
    public string RequestNumber { get; private set; } = null!;
    public Guid BranchId { get; private set; }
    public DateTime NeededByDate { get; private set; }
    public string Priority { get; private set; } = ProductionPriorities.Normal;
    public string Status { get; private set; } = BranchProductionRequestStatuses.Draft;
    public Guid? RequestedByUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public string? Notes { get; private set; }
    public string? DecisionReason { get; private set; }
    public string? RejectionReason { get; private set; }

    private readonly List<AppBranchProductionRequestItem> _items = new();
    public IReadOnlyCollection<AppBranchProductionRequestItem> Items => new ReadOnlyCollection<AppBranchProductionRequestItem>(_items);

    protected AppBranchProductionRequest() { }

    internal AppBranchProductionRequest(
        Guid id,
        string requestNumber,
        Guid branchId,
        DateTime neededByDate,
        string priority,
        Guid? requestedByUserId,
        string? notes = null)
        : base(id)
    {
        RequestNumber = Check.NotNullOrWhiteSpace(requestNumber, nameof(requestNumber), maxLength: 32);
        BranchId = branchId;
        RequestedByUserId = requestedByUserId;
        UpdateHeader(neededByDate, priority, notes);
        Status = BranchProductionRequestStatuses.Draft;
    }

    public void UpdateHeader(DateTime neededByDate, string priority, string? notes)
    {
        EnsureDraft();

        if (!ProductionPriorities.IsValid(priority))
        {
            priority = ProductionPriorities.Normal;
        }

        NeededByDate = neededByDate;
        Priority = priority;
        Notes = notes;
    }

    public AppBranchProductionRequestItem AddItem(
        Guid itemId,
        Guid productId,
        int requestedQuantity,
        string? notes = null)
    {
        EnsureDraft();

        if (_items.Any(i => i.ProductId == productId))
        {
            throw new BusinessException(ProductionErrorCodes.DuplicateRequestProduct)
                .WithData("ProductId", productId);
        }

        var item = new AppBranchProductionRequestItem(itemId, Id, productId, requestedQuantity, notes);
        _items.Add(item);
        return item;
    }

    public void UpdateItem(Guid itemId, int requestedQuantity, string? notes)
    {
        EnsureDraft();

        var item = FindItem(itemId);
        item.SetRequestedQuantity(requestedQuantity);
        item.SetNotes(notes);
    }

    public void RemoveItem(Guid itemId)
    {
        EnsureDraft();
        _items.Remove(FindItem(itemId));
    }

    public void ClearItems()
    {
        EnsureDraft();
        _items.Clear();
    }

    public void Submit()
    {
        if (Status != BranchProductionRequestStatuses.Draft)
        {
            throw InvalidTransition(BranchProductionRequestStatuses.Submitted);
        }
        if (_items.Count == 0)
        {
            throw new BusinessException(ProductionErrorCodes.CannotSubmitEmptyRequest);
        }

        Status = BranchProductionRequestStatuses.Submitted;
    }

    public void Approve(Guid? approvedByUserId, IReadOnlyDictionary<Guid, int> approvedQuantities, string? reason)
    {
        if (Status != BranchProductionRequestStatuses.Submitted)
        {
            throw InvalidTransition(BranchProductionRequestStatuses.Approved);
        }

        var adjusted = false;
        foreach (var item in _items)
        {
            var approved = approvedQuantities.TryGetValue(item.Id, out var quantity)
                ? quantity
                : item.RequestedQuantity;

            if (approved != item.RequestedQuantity)
            {
                adjusted = true;
            }

            item.Approve(approved);
        }

        if (adjusted && string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessException(ProductionErrorCodes.ApprovalAdjustmentReasonRequired);
        }

        ApprovedByUserId = approvedByUserId;
        ApprovedAt = DateTime.UtcNow;
        DecisionReason = reason;
        RejectionReason = null;
        Status = BranchProductionRequestStatuses.Approved;
    }

    public void Reject(Guid? rejectedByUserId, string reason)
    {
        if (Status != BranchProductionRequestStatuses.Submitted)
        {
            throw InvalidTransition(BranchProductionRequestStatuses.Rejected);
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessException(ProductionErrorCodes.RejectionReasonRequired);
        }

        ApprovedByUserId = rejectedByUserId;
        ApprovedAt = DateTime.UtcNow;
        DecisionReason = null;
        RejectionReason = reason.Trim();
        Status = BranchProductionRequestStatuses.Rejected;
    }

    public void Cancel()
    {
        if (!BranchProductionRequestStatuses.IsCancellable(Status))
        {
            throw InvalidTransition(BranchProductionRequestStatuses.Cancelled);
        }

        Status = BranchProductionRequestStatuses.Cancelled;
    }

    public void ReservePlannedQuantity(Guid itemId, int quantity)
    {
        if (!BranchProductionRequestStatuses.IsApprovedDemand(Status))
        {
            throw InvalidTransition(BranchProductionRequestStatuses.PartiallyPlanned);
        }

        FindItem(itemId).ReservePlannedQuantity(quantity);
        RecalculateDemandStatus();
    }

    public void ReleasePlannedQuantity(Guid itemId, int quantity)
    {
        if (!BranchProductionRequestStatuses.IsApprovedDemand(Status))
        {
            throw InvalidTransition(BranchProductionRequestStatuses.Approved);
        }

        FindItem(itemId).ReleasePlannedQuantity(quantity);
        RecalculateDemandStatus();
    }

    public void AddFulfilledQuantity(Guid itemId, int quantity)
    {
        var item = FindItem(itemId);
        item.AddFulfilledQuantity(quantity);
        RecalculateDemandStatus();
    }

    public void CompleteReservedStockDispatch(
        Guid itemId,
        Guid productId,
        int shippedQuantity,
        int receivedQuantity)
    {
        if (!BranchProductionRequestStatuses.IsApprovedDemand(Status))
        {
            throw InvalidTransition(BranchProductionRequestStatuses.PartiallyFulfilled);
        }

        var item = FindItem(itemId);
        if (item.ProductId != productId)
        {
            throw new BusinessException(ProductionErrorCodes.StockDispatchReconciliationMismatch)
                .WithData("ExpectedProductId", item.ProductId)
                .WithData("ReceivedProductId", productId);
        }

        item.CompleteReservedStockDispatch(shippedQuantity, receivedQuantity);
        RecalculateDemandStatus();
    }

    private void RecalculateDemandStatus()
    {
        // A line approved at 0 (a single rejected product) is trivially satisfied —
        // FulfilledQuantity (0) >= ApprovedQuantity (0) — so it must not block the
        // whole request from reaching Fulfilled once every other line is delivered.
        if (_items.All(i => i.FulfilledQuantity >= i.ApprovedQuantity))
        {
            Status = BranchProductionRequestStatuses.Fulfilled;
        }
        else if (_items.Any(i => i.FulfilledQuantity > 0))
        {
            Status = BranchProductionRequestStatuses.PartiallyFulfilled;
        }
        else if (_items.All(i => i.PlannedQuantity >= i.ApprovedQuantity))
        {
            Status = BranchProductionRequestStatuses.Planned;
        }
        else if (_items.Any(i => i.PlannedQuantity > 0))
        {
            Status = BranchProductionRequestStatuses.PartiallyPlanned;
        }
        else
        {
            Status = BranchProductionRequestStatuses.Approved;
        }
    }

    private void EnsureDraft()
    {
        if (Status != BranchProductionRequestStatuses.Draft)
        {
            throw InvalidTransition(BranchProductionRequestStatuses.Draft);
        }
    }

    private AppBranchProductionRequestItem FindItem(Guid itemId)
    {
        return _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new BusinessException(ProductionErrorCodes.RequestItemNotFound)
                .WithData("ItemId", itemId);
    }

    private BusinessException InvalidTransition(string targetStatus) =>
        new BusinessException(ProductionErrorCodes.InvalidRequestStatusTransition)
            .WithData("CurrentStatus", Status)
            .WithData("TargetStatus", targetStatus);
}
