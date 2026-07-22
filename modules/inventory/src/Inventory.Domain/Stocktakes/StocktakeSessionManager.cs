using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.BranchInventory;
using Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Timing;

namespace Inventory.Stocktakes;

public class StocktakeSessionManager : DomainService
{
    private readonly IStocktakeSessionRepository _sessionRepository;
    private readonly IBranchInventoryRepository _inventoryRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly StocktakeManager _stocktakeManager;
    private readonly IClock _clock;

    public StocktakeSessionManager(
        IStocktakeSessionRepository sessionRepository,
        IBranchInventoryRepository inventoryRepository,
        IRepository<AppBranch, Guid> branchRepository,
        StocktakeManager stocktakeManager,
        IClock clock)
    {
        _sessionRepository = sessionRepository;
        _inventoryRepository = inventoryRepository;
        _branchRepository = branchRepository;
        _stocktakeManager = stocktakeManager;
        _clock = clock;
    }

    public async Task<StocktakeSessionStartResult> StartAsync(
        Guid branchId,
        Guid? currentUserId,
        string? currentUserName)
    {
        if (branchId == Guid.Empty)
        {
            throw new BusinessException(InventoryErrorCodes.BranchIdRequired);
        }

        var currentOpenSession = await _sessionRepository.FindOpenByBranchAsync(branchId);
        if (currentOpenSession is not null)
        {
            return new StocktakeSessionStartResult(currentOpenSession, IsNew: false);
        }

        await _branchRepository.GetAsync(branchId);
        var rows = await _inventoryRepository.GetStocktakeRowsAsync(branchId);
        var seeds = rows.ConvertAll(row => new StocktakeSessionLineSeed(
            row.Inventory.Id,
            row.Product.Id,
            row.Product.Name,
            row.Product.SKU,
            row.Product.Unit,
            row.Inventory.QuantityOnHand,
            row.Product.ShelfLifeDays.HasValue,
            row.Inventory.ConcurrencyStamp));

        var session = new AppStocktakeSession(
            GuidGenerator.Create(),
            branchId,
            _clock.Now,
            currentUserId,
            currentUserName,
            seeds,
            GuidGenerator.Create);

        return new StocktakeSessionStartResult(session, IsNew: true);
    }

    public void UpdateDraft(
        AppStocktakeSession session,
        IReadOnlyCollection<StocktakeSessionDraftLine> updates,
        string? notes,
        string? concurrencyStamp)
    {
        EnsureConcurrencyStamp(session, concurrencyStamp);
        session.UpdateDraft(updates, notes, _clock.Now.Date);
    }

    public async Task SubmitAsync(
        AppStocktakeSession session,
        IReadOnlyCollection<StocktakeSessionDraftLine> updates,
        string notes,
        string? concurrencyStamp,
        Guid? currentUserId,
        string? currentUserName)
    {
        EnsureConcurrencyStamp(session, concurrencyStamp);
        session.EnsureDraft();
        var counts = BuildCompletionCounts(session, updates);
        await _stocktakeManager.ValidateAsync(session.BranchId, counts, notes);
        session.UpdateDraft(updates, notes, _clock.Now.Date);
        session.Submit(_clock.Now, currentUserId, currentUserName);
    }

    public async Task<StocktakePostingResult> ApproveAsync(
        AppStocktakeSession session,
        string? concurrencyStamp,
        Guid? currentUserId,
        string? currentUserName,
        string? reviewNotes)
    {
        EnsureConcurrencyStamp(session, concurrencyStamp);
        session.EnsurePendingReview();
        var counts = BuildStoredCounts(session);
        var result = await _stocktakeManager.PostAsync(session.BranchId, counts, session.Notes);
        session.Approve(result, _clock.Now, currentUserId, currentUserName, reviewNotes);
        return result;
    }

    public void Reject(
        AppStocktakeSession session,
        string? concurrencyStamp,
        Guid? currentUserId,
        string? currentUserName,
        string reviewNotes)
    {
        EnsureConcurrencyStamp(session, concurrencyStamp);
        session.Reject(_clock.Now, currentUserId, currentUserName, reviewNotes);
    }

    public void Cancel(AppStocktakeSession session, string? concurrencyStamp, Guid? currentUserId)
    {
        EnsureConcurrencyStamp(session, concurrencyStamp);
        session.Cancel(_clock.Now, currentUserId);
    }

    public static void EnsureConcurrencyStamp(AppStocktakeSession session, string? expectedStamp)
    {
        Check.NotNull(session, nameof(session));
        if (string.IsNullOrWhiteSpace(expectedStamp)
            || !string.Equals(session.ConcurrencyStamp, expectedStamp, StringComparison.Ordinal))
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeSessionConcurrency);
        }
    }

    private static List<StocktakeCount> BuildCompletionCounts(
        AppStocktakeSession session,
        IReadOnlyCollection<StocktakeSessionDraftLine> updates)
    {
        var updatesById = new Dictionary<Guid, StocktakeSessionDraftLine>();
        foreach (var update in updates)
        {
            if (!updatesById.TryAdd(update.LineId, update))
            {
                throw new BusinessException(InventoryErrorCodes.StocktakeDuplicateLine);
            }
        }

        if (updatesById.Count != session.TotalLineCount)
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeSessionLineMismatch);
        }

        var counts = new List<StocktakeCount>(session.TotalLineCount);
        foreach (var line in session.Lines)
        {
            if (!updatesById.TryGetValue(line.Id, out var update))
            {
                throw new BusinessException(InventoryErrorCodes.StocktakeSessionLineMismatch);
            }

            if (!update.CountedQuantity.HasValue)
            {
                throw new BusinessException(InventoryErrorCodes.StocktakeIncomplete)
                    .WithData("Expected", session.TotalLineCount)
                    .WithData("Submitted", updatesById.Count(item => item.Value.CountedQuantity.HasValue));
            }

            counts.Add(new StocktakeCount(
                line.InventoryId,
                update.CountedQuantity.Value,
                line.InventoryConcurrencyStamp,
                update.Reason,
                update.ReasonNotes,
                update.ProductionDate));
        }

        return counts;
    }

    private static List<StocktakeCount> BuildStoredCounts(AppStocktakeSession session)
    {
        var counts = new List<StocktakeCount>(session.TotalLineCount);
        foreach (var line in session.Lines)
        {
            if (!line.CountedQuantity.HasValue)
            {
                throw new BusinessException(InventoryErrorCodes.StocktakeIncomplete);
            }

            counts.Add(new StocktakeCount(
                line.InventoryId,
                line.CountedQuantity.Value,
                line.InventoryConcurrencyStamp,
                line.Reason,
                line.ReasonNotes,
                line.ProductionDate));
        }

        return counts;
    }
}
