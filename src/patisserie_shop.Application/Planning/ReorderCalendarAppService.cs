using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Localization;
using Intelligence.Permissions;
using Intelligence.Velocity;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Permissions;
using Inventory.StockBatches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Operations;
using Operations.Entities;
using Operations.Localization;
using Volo.Abp.Domain.Repositories;

namespace patisserie_shop.Planning;

/// <summary>
/// Composes three deterministic forward signals into one month of calendar events,
/// the same cross-module in-memory composition pattern as
/// <see cref="patisserie_shop.Analytics.SalesAnalyticsAppService"/>: the velocity
/// read-model + ForecastWalker (stockouts), the batch ledger (expiries) and open
/// purchase orders (deliveries). No AI, no queryable LINQ in the service — the
/// repositories own the queries; this service only walks the loaded rows and maps.
/// </summary>
[Authorize(IntelligencePermissions.DecisionLogs.Default)]
public class ReorderCalendarAppService : patisserie_shopAppService, IReorderCalendarAppService
{
    /// <summary>Hard cap on the events returned in one response (newest layers trimmed).</summary>
    private const int MaxEvents = 500;

    /// <summary>Upper bound on velocity rows pulled for the stockout walk.</summary>
    private const int VelocityRowCap = 5000;

    private readonly IProductVelocityRepository _velocityRepository;
    private readonly IStockBatchRepository _batchRepository;
    private readonly IRepository<AppPurchaseOrder, Guid> _purchaseOrderRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppSupplier, Guid> _supplierRepository;
    private readonly IBranchInventoryAppService _branchInventoryAppService;
    private readonly IStringLocalizer<IntelligenceResource> _intelligenceLocalizer;
    private readonly IStringLocalizer<OperationsResource> _operationsLocalizer;

    public ReorderCalendarAppService(
        IProductVelocityRepository velocityRepository,
        IStockBatchRepository batchRepository,
        IRepository<AppPurchaseOrder, Guid> purchaseOrderRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppSupplier, Guid> supplierRepository,
        IBranchInventoryAppService branchInventoryAppService,
        IStringLocalizer<IntelligenceResource> intelligenceLocalizer,
        IStringLocalizer<OperationsResource> operationsLocalizer)
    {
        _velocityRepository = velocityRepository;
        _batchRepository = batchRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _branchRepository = branchRepository;
        _supplierRepository = supplierRepository;
        _branchInventoryAppService = branchInventoryAppService;
        _intelligenceLocalizer = intelligenceLocalizer;
        _operationsLocalizer = operationsLocalizer;
    }

    public async Task<ReorderCalendarDto> GetAsync(GetReorderCalendarInput input)
    {
        var todayUtc = Clock.Now.ToUniversalTime().Date;

        // ── Window: the requested month, padded to the visible Sunday-first 6-week grid ──
        var monthAnchor = (input.Month ?? todayUtc).Date;
        var monthStart = new DateTime(monthAnchor.Year, monthAnchor.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var gridStart = monthStart.AddDays(-(int)monthStart.DayOfWeek);          // back to Sunday
        var gridEnd = monthEnd.AddDays(6 - (int)monthEnd.DayOfWeek);             // forward to Saturday

        var scope = await ResolveBranchScopeAsync(input.BranchId);

        var dto = new ReorderCalendarDto
        {
            MonthStart = monthStart,
            MonthEnd = monthEnd,
            GridStart = gridStart,
            GridEnd = gridEnd,
            TodayUtc = todayUtc
        };

        // An empty scope (manager asked for a branch they don't manage) → nothing to show.
        if (scope != null && scope.Count == 0)
        {
            return dto;
        }

        var branchNames = (await _branchRepository.GetListAsync())
            .ToDictionary(b => b.Id, b => b.DisplayName);

        var events = new List<CalendarEventDto>();
        events.AddRange(await BuildStockoutEventsAsync(scope, branchNames, todayUtc, gridEnd));
        events.AddRange(await BuildExpiryEventsAsync(scope, branchNames, gridStart, gridEnd));
        events.AddRange(await BuildDeliveryEventsAsync(scope, branchNames, gridStart, gridEnd));

        dto.StockoutCount = events.Count(e => e.EventType == CalendarEventTypes.Stockout);
        dto.ExpiryCount = events.Count(e => e.EventType == CalendarEventTypes.Expiry);
        dto.DeliveryCount = events.Count(e => e.EventType == CalendarEventTypes.Delivery);

        var ordered = events
            .OrderBy(e => e.Date)
            .ThenBy(e => EventTypeRank(e.EventType))
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ordered.Count > MaxEvents)
        {
            dto.Truncated = true;
            ordered = ordered.Take(MaxEvents).ToList();
        }

        dto.Events = ordered;
        return dto;
    }

    /// <summary>
    /// Stockout layer: for every in-scope velocity row with a usable demand signal,
    /// walk the weekday-indexed forecast from tomorrow over the remaining horizon and
    /// emit a "predicted stockout" event on (today + daysUntilDepletion) when that
    /// date lands on or before the grid's end. Rows with zero velocity or that never
    /// deplete inside the horizon are skipped — deterministic, explainable arithmetic.
    /// </summary>
    private async Task<List<CalendarEventDto>> BuildStockoutEventsAsync(
        IReadOnlyCollection<Guid>? scope,
        Dictionary<Guid, string> branchNames,
        DateTime todayUtc,
        DateTime gridEnd)
    {
        var result = new List<CalendarEventDto>();

        // Past months: the forecast can only point forward, so there is nothing to add.
        var horizon = (gridEnd - todayUtc).Days;
        if (horizon <= 0)
        {
            return result;
        }

        var rows = await _velocityRepository.GetFilteredListAsync(
            filter: null,
            branchId: null,
            scopedBranchIds: scope,
            sorting: string.Empty,
            skipCount: 0,
            maxResultCount: VelocityRowCap);

        var tomorrow = todayUtc.AddDays(1);

        foreach (var row in rows)
        {
            var velocity = row.Velocity;
            if (velocity.AvgDailySales30 <= 0 || row.CurrentStock <= 0)
            {
                continue;
            }

            var indices = velocity.GetWeekdayIndices();
            var days = ForecastWalker.DaysUntilDepletion(
                row.CurrentStock, velocity.AvgDailySales30, indices, tomorrow.DayOfWeek, horizon);

            if (days is not int d)
            {
                continue; // survives the whole horizon
            }

            var date = todayUtc.AddDays(d);
            if (date > gridEnd)
            {
                continue;
            }

            var branchName = branchNames.GetValueOrDefault(
                velocity.BranchId,
                _intelligenceLocalizer["ReorderCalendar:DeletedBranch"].Value);
            var averageDailySales = velocity.AvgDailySales30.ToString("0.##", CultureInfo.CurrentCulture);
            result.Add(new CalendarEventDto
            {
                Date = date,
                EventType = CalendarEventTypes.Stockout,
                Title = row.ProductName,
                Detail = _intelligenceLocalizer[
                    "ReorderCalendar:Event:StockoutDetail",
                    branchName,
                    row.CurrentStock,
                    averageDailySales].Value,
                ProductId = velocity.ProductId,
                BranchId = velocity.BranchId,
                BranchName = branchName,
                Accent = "danger"
            });
        }

        return result;
    }

    /// <summary>
    /// Expiry layer: live batches of active products whose ExpiryDate lands inside the
    /// visible grid window, one event per batch on its expiry day.
    /// </summary>
    private async Task<List<CalendarEventDto>> BuildExpiryEventsAsync(
        IReadOnlyCollection<Guid>? scope,
        Dictionary<Guid, string> branchNames,
        DateTime gridStart,
        DateTime gridEnd)
    {
        var rows = await _batchRepository.GetExpiringInWindowAsync(gridStart, gridEnd, scope, MaxEvents);

        return rows.ConvertAll(r =>
        {
            var branchName = r.Branch?.DisplayName ?? branchNames.GetValueOrDefault(
                r.Batch.BranchId,
                _intelligenceLocalizer["ReorderCalendar:DeletedBranch"].Value);
            return new CalendarEventDto
            {
                Date = r.Batch.ExpiryDate.Date,
                EventType = CalendarEventTypes.Expiry,
                Title = r.Product.DisplayName,
                Detail = _intelligenceLocalizer[
                    "ReorderCalendar:Event:ExpiryDetail",
                    branchName,
                    r.Batch.QuantityRemaining,
                    r.Batch.BatchNumber].Value,
                ProductId = r.Batch.ProductId,
                BranchId = r.Batch.BranchId,
                BranchName = branchName,
                Accent = "warn"
            };
        });
    }

    /// <summary>
    /// Delivery layer: open purchase orders (not cancelled, not yet fully received)
    /// with an ExpectedDeliveryDate inside the window, one event per PO on that date.
    /// Uses the generic repository's predicate query — the same way the dashboards
    /// read PO status; no queryable LINQ leaks into the service body.
    /// </summary>
    private async Task<List<CalendarEventDto>> BuildDeliveryEventsAsync(
        IReadOnlyCollection<Guid>? scope,
        Dictionary<Guid, string> branchNames,
        DateTime gridStart,
        DateTime gridEnd)
    {
        var rangeEnd = gridEnd.AddDays(1); // exclusive upper bound on a DateTime comparison

        var orders = await _purchaseOrderRepository.GetListAsync(po =>
            po.ExpectedDeliveryDate != null
            && po.ExpectedDeliveryDate >= gridStart
            && po.ExpectedDeliveryDate < rangeEnd
            && po.Status != PurchaseOrderStatuses.Cancelled
            && po.Status != PurchaseOrderStatuses.Received);

        if (scope != null)
        {
            var allowed = scope.ToHashSet();
            orders = orders.Where(po => allowed.Contains(po.DestBranchId)).ToList();
        }

        if (orders.Count == 0)
        {
            return new List<CalendarEventDto>();
        }

        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var supplierNames = (await _supplierRepository.GetListAsync(s => supplierIds.Contains(s.Id)))
            .ToDictionary(s => s.Id, s => s.Name);

        return orders.ConvertAll(po =>
        {
            var branchName = branchNames.GetValueOrDefault(
                po.DestBranchId,
                _intelligenceLocalizer["ReorderCalendar:DeletedBranch"].Value);
            var supplierName = supplierNames.GetValueOrDefault(
                po.SupplierId,
                _intelligenceLocalizer["ReorderCalendar:UnknownSupplier"].Value);
            var statusLabel = _operationsLocalizer[$"Status:{po.Status}"].Value;
            return new CalendarEventDto
            {
                Date = po.ExpectedDeliveryDate!.Value.Date,
                EventType = CalendarEventTypes.Delivery,
                Title = po.PONumber,
                Detail = _intelligenceLocalizer[
                    "ReorderCalendar:Event:DeliveryDetail",
                    supplierName,
                    branchName,
                    statusLabel].Value,
                ProductId = null,
                BranchId = po.DestBranchId,
                BranchName = branchName,
                Accent = "success"
            };
        });
    }

    /// <summary>
    /// Mirrors ProductVelocityAppService scoping: ManageAll → unscoped (null);
    /// otherwise the caller's accessible branch ids. An explicit BranchId is
    /// intersected with that scope, never widened beyond it.
    /// </summary>
    private async Task<IReadOnlyCollection<Guid>?> ResolveBranchScopeAsync(Guid? branchId)
    {
        IReadOnlyCollection<Guid>? scope = null;

        if (!await AuthorizationService.IsGrantedAsync(InventoryPermissions.BranchInventory.ManageAll))
        {
            scope = await _branchInventoryAppService.GetAccessibleBranchIdsAsync();
        }

        if (!branchId.HasValue)
        {
            return scope;
        }

        if (scope == null || scope.Contains(branchId.Value))
        {
            return new[] { branchId.Value };
        }

        // Requested branch is outside the caller's scope → empty result, not an error.
        return Array.Empty<Guid>();
    }

    // Same day → stockouts first (most urgent), then expiries, then deliveries.
    private static int EventTypeRank(string eventType) => eventType switch
    {
        CalendarEventTypes.Stockout => 0,
        CalendarEventTypes.Expiry => 1,
        CalendarEventTypes.Delivery => 2,
        _ => 3
    };
}
