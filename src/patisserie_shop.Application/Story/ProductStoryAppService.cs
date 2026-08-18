using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Intelligence.Decisions;
using Intelligence.Localization;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Localization;
using Inventory.Permissions;
using Inventory.StockMovements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Operations.Sales;
using Volo.Abp.Domain.Repositories;

namespace patisserie_shop.Story;

/// <summary>
/// Composes the three immutable per-product ledgers — Inventory stock movements,
/// Intelligence decision logs and Inventory stock batches — into one merged,
/// newest-first timeline for a single product at a single branch. Same host-level,
/// in-memory composition pattern as <see cref="patisserie_shop.Analytics.SalesAnalyticsAppService"/>:
/// every query runs in a module repository; this service only merges, sorts and maps.
/// Branch access follows the BranchInventory precedent: <see cref="BranchAccessChecker"/>
/// throws for a branch the caller doesn't manage.
/// </summary>
[Authorize(InventoryPermissions.BranchInventory.Default)]
public class ProductStoryAppService : patisserie_shopAppService, IProductStoryAppService
{
    private const int DefaultMaxEvents = 100;
    private const int MaxEventsCap = 200;

    private readonly IStockMovementRepository _movementRepository;
    private readonly IDecisionLogRepository _decisionLogRepository;
    private readonly IRepository<AppStockBatch, Guid> _batchRepository;
    private readonly IRepository<AppBranchInventory, Guid> _inventoryRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly BranchAccessChecker _branchAccess;
    private readonly IStringLocalizer<InventoryResource> _inventoryLocalizer;
    private readonly IStringLocalizer<IntelligenceResource> _intelligenceLocalizer;

    public ProductStoryAppService(
        IStockMovementRepository movementRepository,
        IDecisionLogRepository decisionLogRepository,
        IRepository<AppStockBatch, Guid> batchRepository,
        IRepository<AppBranchInventory, Guid> inventoryRepository,
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppBranch, Guid> branchRepository,
        ISaleRepository saleRepository,
        BranchAccessChecker branchAccess,
        IStringLocalizer<InventoryResource> inventoryLocalizer,
        IStringLocalizer<IntelligenceResource> intelligenceLocalizer)
    {
        _movementRepository = movementRepository;
        _decisionLogRepository = decisionLogRepository;
        _batchRepository = batchRepository;
        _inventoryRepository = inventoryRepository;
        _productRepository = productRepository;
        _branchRepository = branchRepository;
        _saleRepository = saleRepository;
        _branchAccess = branchAccess;
        _inventoryLocalizer = inventoryLocalizer;
        _intelligenceLocalizer = intelligenceLocalizer;
    }

    public async Task<ProductStoryDto> GetAsync(GetProductStoryInput input)
    {
        var maxEvents = NormalizeMaxEvents(input.MaxEvents);

        // Inaccessible branch → BusinessException(BranchAccessDenied), the same
        // behavior as every BranchInventory endpoint.
        await _branchAccess.EnsureAccessAsync(input.BranchId);

        var product = await _productRepository.GetAsync(input.ProductId);
        var branch = await _branchRepository.GetAsync(input.BranchId);

        var inventory = await _inventoryRepository.FindAsync(
            x => x.BranchId == input.BranchId && x.ProductId == input.ProductId);

        // ── Source 1: stock movements (purchases, sales, transfers, adjustments) ──
        var movements = await _movementRepository.GetFilteredListAsync(
            new StockMovementListFilter
            {
                BranchId = input.BranchId,
                ProductId = input.ProductId
            },
            sorting: string.Empty,          // repository default: CreationTime desc
            skipCount: 0,
            maxResultCount: maxEvents);

        // ── Source 2: decision logs (with status + 48h outcome) ──
        var decisions = await _decisionLogRepository.GetFilteredListAsync(
            filter: null,
            decisionType: null,
            status: null,
            branchId: input.BranchId,
            productId: input.ProductId,
            fromDate: null,
            toDate: null,
            scopedBranchIds: null,          // already authorized via EnsureAccessAsync
            sorting: string.Empty,          // repository default: CreationTime desc
            skipCount: 0,
            maxResultCount: maxEvents);

        // ── Source 3: stock batches (received lots with their expiry fate) ──
        var batches = await _batchRepository.GetListAsync(
            b => b.BranchId == input.BranchId && b.ProductId == input.ProductId);

        // ── Header stat: units sold in the trailing 30 days at this branch ──
        var todayStart = DateTime.UtcNow.Date;
        var salesTotals = await _saleRepository.GetProductSalesTotalsAsync(
            todayStart.AddDays(-29),
            todayStart.AddDays(1),
            new[] { input.BranchId });
        var sold30 = salesTotals
            .FirstOrDefault(p => p.ProductId == input.ProductId)?.TotalQuantitySold ?? 0;

        // ── Merge + sort newest-first in memory, cap at MaxEvents ──
        var nowUtc = DateTime.UtcNow;
        var events = new List<StoryEventDto>(movements.Count + decisions.Count + batches.Count);
        events.AddRange(movements.Select(ToMovementEvent));
        events.AddRange(decisions.Select(ToDecisionEvent));
        events.AddRange(batches.Select(b => ToBatchEvent(b, nowUtc)));

        return new ProductStoryDto
        {
            ProductId = product.Id,
            BranchId = branch.Id,
            ProductName = product.DisplayName,
            SKU = product.SKU,
            BranchName = branch.DisplayName,
            CurrentStock = inventory?.QuantityOnHand ?? 0,
            TotalSold30Days = sold30,
            BatchCount = batches.Count,
            DaysSinceLastSale = inventory?.LastSoldDate == null
                ? null
                : (int)(nowUtc.Date - inventory.LastSoldDate.Value.Date).TotalDays,
            Events = events
                .OrderByDescending(e => e.OccurredAt)
                .Take(maxEvents)
                .ToList()
        };
    }

    private StoryEventDto ToMovementEvent(StockMovementWithContext row)
    {
        var m = row.Movement;
        var delta = m.QuantityAfter - m.QuantityBefore;
        var qty = Math.Abs(delta);

        var (eventType, title) = m.MovementType switch
        {
            StockMovementTypes.Purchase => (StoryEventTypes.Purchase, L["ProductStory:Movement:Purchase", qty].Value),
            StockMovementTypes.Sale => (StoryEventTypes.Sale, L["ProductStory:Movement:Sale", qty].Value),
            StockMovementTypes.TransferIn => (StoryEventTypes.TransferIn, L["ProductStory:Movement:TransferIn", qty].Value),
            StockMovementTypes.TransferOut => (StoryEventTypes.TransferOut, L["ProductStory:Movement:TransferOut", qty].Value),
            StockMovementTypes.WriteOff => (StoryEventTypes.WriteOff, L["ProductStory:Movement:WriteOff", qty].Value),
            _ => (StoryEventTypes.Adjustment, delta >= 0
                ? L["ProductStory:Movement:AdjustedUp", _inventoryLocalizer[$"MovementType:{m.MovementType}"], qty].Value
                : L["ProductStory:Movement:AdjustedDown", _inventoryLocalizer[$"MovementType:{m.MovementType}"], qty].Value)
        };

        var detail = new StringBuilder(L[
            "ProductStory:Movement:StockChange",
            m.QuantityBefore,
            m.QuantityAfter].Value);
        if (!string.IsNullOrWhiteSpace(m.Notes))
        {
            detail.Append(' ').Append(FriendlyMovementNotes(m.Notes));
        }

        return new StoryEventDto
        {
            OccurredAt = m.CreationTime,
            EventType = eventType,
            Title = title,
            Detail = detail.ToString(),
            QuantityDelta = delta
        };
    }

    private StoryEventDto ToDecisionEvent(DecisionLogWithRuleName row)
    {
        var d = row.DecisionLog;

        var detail = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(row.RuleName))
        {
            detail.Append('[').Append(row.RuleName).Append("] ");
        }
        detail.Append(d.Reasoning);
        detail.Append(' ').Append(L[
            "ProductStory:Decision:Status",
            _intelligenceLocalizer[$"DecisionStatus:{d.Status}"]]);
        if (d.Outcome != null)
        {
            detail.Append(' ').Append(L[
                "ProductStory:Decision:Outcome",
                _intelligenceLocalizer[$"Outcome:{d.Outcome}"]]);
        }

        return new StoryEventDto
        {
            OccurredAt = d.CreationTime,
            EventType = StoryEventTypes.Decision,
            Title = L[
                "ProductStory:Decision:Title",
                _intelligenceLocalizer[$"DecisionType:{d.DecisionType}"]],
            Detail = detail.ToString(),
            QuantityDelta = null
        };
    }

    private StoryEventDto ToBatchEvent(AppStockBatch batch, DateTime nowUtc)
    {
        string detail;
        if (batch.IsExpired(nowUtc))
        {
            detail = batch.QuantityRemaining > 0
                ? L["ProductStory:Batch:ExpiredRemaining", batch.QuantityRemaining]
                : L["ProductStory:Batch:Consumed"];
        }
        else
        {
            detail = batch.IsDepleted
                ? L["ProductStory:Batch:Consumed"]
                : L["ProductStory:Batch:Remaining", batch.QuantityRemaining, batch.QuantityReceived];
        }

        return new StoryEventDto
        {
            OccurredAt = batch.CreationTime,
            EventType = StoryEventTypes.Batch,
            Title = L[
                "ProductStory:Batch:Received",
                batch.BatchNumber,
                batch.QuantityReceived,
                batch.ExpiryDate.ToString("d", CultureInfo.CurrentCulture)],
            Detail = detail,
            QuantityDelta = null
        };
    }

    private static int NormalizeMaxEvents(int maxEvents)
        => maxEvents <= 0 ? DefaultMaxEvents : Math.Min(maxEvents, MaxEventsCap);

    private string FriendlyMovementNotes(string notes)
    {
        if (StocktakeMovementNote.TryParse(notes, out var reason, out var text))
        {
            return $"{_inventoryLocalizer[$"Stocktake:Reason:{reason}"]} — {text}";
        }

        return notes.Trim();
    }
}
