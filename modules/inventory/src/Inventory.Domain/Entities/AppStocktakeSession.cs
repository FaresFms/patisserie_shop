using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.BranchInventory;
using Inventory.Stocktakes;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Inventory.Entities;

public class AppStocktakeSession : FullAuditedAggregateRoot<Guid>
{
    public Guid BranchId { get; private set; }
    public string Status { get; private set; } = null!;
    public DateTime SnapshotAt { get; private set; }
    public Guid? StartedBy { get; private set; }
    public string? StartedByName { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public Guid? SubmittedBy { get; private set; }
    public string? SubmittedByName { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public string? ReviewedByName { get; private set; }
    public string? ReviewNotes { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public Guid? MovementReferenceId { get; private set; }
    public int TotalLineCount { get; private set; }
    public int CountedLineCount { get; private set; }
    public int DifferenceLineCount { get; private set; }
    public int AdjustedLineCount { get; private set; }
    public int MatchedLineCount { get; private set; }
    public int WriteOffLineCount { get; private set; }
    public int ManualAdjustmentLineCount { get; private set; }
    public int ShortageQuantity { get; private set; }
    public int OverageQuantity { get; private set; }
    public ICollection<AppStocktakeLine> Lines { get; private set; }

    protected AppStocktakeSession()
    {
        Lines = new List<AppStocktakeLine>();
    }

    internal AppStocktakeSession(
        Guid id,
        Guid branchId,
        DateTime snapshotAt,
        Guid? startedBy,
        string? startedByName,
        IReadOnlyCollection<StocktakeSessionLineSeed> lineSeeds,
        Func<Guid> newLineId)
        : base(id)
    {
        if (branchId == Guid.Empty)
        {
            throw new BusinessException(InventoryErrorCodes.BranchIdRequired);
        }

        if (lineSeeds.Count == 0)
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeEmpty);
        }

        BranchId = branchId;
        SnapshotAt = snapshotAt;
        StartedBy = startedBy;
        StartedByName = NormalizeUserName(startedByName);
        Status = StocktakeSessionStatuses.Draft;
        Lines = lineSeeds
            .Select(seed => new AppStocktakeLine(
                newLineId(),
                id,
                seed.InventoryId,
                seed.ProductId,
                seed.ProductName,
                seed.ProductSku,
                seed.ProductUnit,
                seed.ExpectedQuantity,
                seed.IsPerishable,
                seed.InventoryConcurrencyStamp))
            .ToList();
        RecalculateDraftSummary();
    }

    internal void UpdateDraft(
        IReadOnlyCollection<StocktakeSessionDraftLine> updates,
        string? notes,
        DateTime today)
    {
        EnsureDraft();
        var updatesById = new Dictionary<Guid, StocktakeSessionDraftLine>();
        foreach (var update in updates)
        {
            if (!updatesById.TryAdd(update.LineId, update))
            {
                throw new BusinessException(InventoryErrorCodes.StocktakeDuplicateLine);
            }
        }

        if (updatesById.Count != Lines.Count || Lines.Any(line => !updatesById.ContainsKey(line.Id)))
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeSessionLineMismatch);
        }

        notes = notes?.Trim();
        if (notes?.Length > 300)
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeNotesTooLong);
        }

        foreach (var line in Lines)
        {
            var update = updatesById[line.Id];
            line.UpdateDraft(
                update.CountedQuantity,
                update.Reason,
                update.ReasonNotes,
                update.ProductionDate,
                today);
        }

        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes;
        RecalculateDraftSummary();
    }

    internal void Submit(DateTime submittedAt, Guid? submittedBy, string? submittedByName)
    {
        EnsureDraft();
        Status = StocktakeSessionStatuses.PendingReview;
        SubmittedAt = submittedAt;
        SubmittedBy = submittedBy;
        SubmittedByName = NormalizeUserName(submittedByName);
    }

    internal void Approve(
        StocktakePostingResult result,
        DateTime reviewedAt,
        Guid? reviewedBy,
        string? reviewedByName,
        string? reviewNotes)
    {
        EnsurePendingReview();
        Status = StocktakeSessionStatuses.Completed;
        SetReview(reviewedAt, reviewedBy, reviewedByName, reviewNotes, requireNotes: false);
        ClosedAt = reviewedAt;
        ClosedBy = reviewedBy;
        MovementReferenceId = result.ReferenceId;
        CountedLineCount = result.CountedLineCount;
        AdjustedLineCount = result.AdjustedLineCount;
        MatchedLineCount = result.MatchedLineCount;
        WriteOffLineCount = result.WriteOffLineCount;
        ManualAdjustmentLineCount = result.ManualAdjustmentLineCount;
        ShortageQuantity = result.ShortageQuantity;
        OverageQuantity = result.OverageQuantity;
        DifferenceLineCount = result.AdjustedLineCount;
    }

    internal void Reject(
        DateTime reviewedAt,
        Guid? reviewedBy,
        string? reviewedByName,
        string reviewNotes)
    {
        EnsurePendingReview();
        SetReview(reviewedAt, reviewedBy, reviewedByName, reviewNotes, requireNotes: true);
        Status = StocktakeSessionStatuses.Rejected;
        ClosedAt = reviewedAt;
        ClosedBy = reviewedBy;
    }

    internal void Cancel(DateTime closedAt, Guid? closedBy)
    {
        EnsureDraft();
        Status = StocktakeSessionStatuses.Cancelled;
        ClosedAt = closedAt;
        ClosedBy = closedBy;
    }

    internal void EnsureDraft()
    {
        if (Status != StocktakeSessionStatuses.Draft)
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeSessionNotDraft);
        }
    }

    internal void EnsurePendingReview()
    {
        if (Status != StocktakeSessionStatuses.PendingReview)
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeSessionNotPendingReview);
        }
    }

    private void SetReview(
        DateTime reviewedAt,
        Guid? reviewedBy,
        string? reviewedByName,
        string? reviewNotes,
        bool requireNotes)
    {
        reviewNotes = reviewNotes?.Trim();
        if (requireNotes && string.IsNullOrWhiteSpace(reviewNotes))
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeReviewNotesRequired);
        }

        if (reviewNotes?.Length > 300)
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeNotesTooLong);
        }

        ReviewedAt = reviewedAt;
        ReviewedBy = reviewedBy;
        ReviewedByName = NormalizeUserName(reviewedByName);
        ReviewNotes = string.IsNullOrWhiteSpace(reviewNotes) ? null : reviewNotes;
    }

    private static string? NormalizeUserName(string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length <= 128 ? value : value[..128];
    }

    private void RecalculateDraftSummary()
    {
        TotalLineCount = Lines.Count;
        CountedLineCount = Lines.Count(line => line.CountedQuantity.HasValue);
        DifferenceLineCount = Lines.Count(line => line.Difference is not null and not 0);
        MatchedLineCount = Lines.Count(line => line.Difference == 0);
        ShortageQuantity = Lines
            .Where(line => line.Difference < 0)
            .Sum(line => -line.Difference!.Value);
        OverageQuantity = Lines
            .Where(line => line.Difference > 0)
            .Sum(line => line.Difference!.Value);
    }
}
