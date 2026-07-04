using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Decisions;
using Intelligence.Entities;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Permissions;
using Inventory.StockBatches;
using Inventory.StockMovements;
using Microsoft.AspNetCore.Authorization;
using Operations.Sales;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

namespace patisserie_shop.Dashboard;

/// <summary>
/// Composes one explainable 0–100 health score per branch from the SAME data
/// sources the dashboards already aggregate (branch inventory rows, decision logs,
/// write-off movements, sales, stock batches). Pure deterministic arithmetic — no
/// AI/ML, every component carries a human Detail string (thesis: fully explainable).
/// Cross-branch aggregates are pulled ONCE and grouped in memory; there are no
/// per-branch (N+1) queries — same host-level composition pattern as
/// <see cref="AdminDashboardAppService"/> and the Analytics services.
/// </summary>
[Authorize(InventoryPermissions.BranchInventory.Default)]
public class BranchHealthAppService : patisserie_shopAppService, IBranchHealthAppService
{
    // ── Component weights (sum = 100) ──────────────────────────────────────
    private const int StockHealthMax = 30;
    private const int StockoutMax = 20;
    private const int PendingLoadMax = 15;
    private const int WasteRatioMax = 15;
    private const int ExpiryRiskMax = 10;
    private const int ResponsivenessMax = 10;

    // ── Deterministic thresholds ───────────────────────────────────────────
    private const int ExcessStockHighThreshold = 100; // mirrors the dashboards
    private const int StockoutPenaltyPerRow = 4;       // −4 pts per out-of-stock row
    private const int PendingGrace = 3;                // first 3 pending are free
    private const int PendingPenaltyPerExtra = 1;      // −1 pt per pending over grace
    private const int ExpiryWindowDays = 3;            // "expiring soon" horizon
    private const int ExpiryPenaltyPerBatch = 2;       // −2 pts per expiring batch
    private const int WasteRatioCapMultiplier = 10;    // ≥10% waste-to-sales → 0 pts
    private const int ResponsivenessAgeHours = 48;     // decisions older than this count
    private const int WasteWindowDays = 30;

    // ── Grade cutoffs ──────────────────────────────────────────────────────
    private const int GradeACutoff = 85;
    private const int GradeBCutoff = 70;
    private const int GradeCCutoff = 55;

    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IBranchInventoryRepository _inventoryRepository;
    private readonly IDecisionLogRepository _decisionLogRepository;
    private readonly IStockMovementRepository _movementRepository;
    private readonly IStockBatchRepository _batchRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly BranchAccessChecker _branchAccess;
    private readonly ISettingProvider _settingProvider;

    public BranchHealthAppService(
        IRepository<AppBranch, Guid> branchRepository,
        IBranchInventoryRepository inventoryRepository,
        IDecisionLogRepository decisionLogRepository,
        IStockMovementRepository movementRepository,
        IStockBatchRepository batchRepository,
        ISaleRepository saleRepository,
        BranchAccessChecker branchAccess,
        ISettingProvider settingProvider)
    {
        _branchRepository = branchRepository;
        _inventoryRepository = inventoryRepository;
        _decisionLogRepository = decisionLogRepository;
        _movementRepository = movementRepository;
        _batchRepository = batchRepository;
        _saleRepository = saleRepository;
        _branchAccess = branchAccess;
        _settingProvider = settingProvider;
    }

    public async Task<List<BranchHealthDto>> GetBranchHealthAsync()
    {
        // Scope: admin (ManageAll) → all active branches; manager → their active
        // branches. Same helper the BranchInventory endpoints use.
        var accessibleIds = await _branchAccess.GetAccessibleBranchIdsAsync();
        if (accessibleIds.Count == 0)
        {
            return new List<BranchHealthDto>();
        }

        var branchScope = accessibleIds.ToArray();
        var branches = (await _branchRepository.GetListAsync(b => b.IsActive))
            .Where(b => accessibleIds.Contains(b.Id))
            .ToList();

        var nowUtc = DateTime.UtcNow;
        var todayStart = nowUtc.Date;
        var wasteFrom = todayStart.AddDays(-(WasteWindowDays - 1));
        var windowEnd = todayStart.AddDays(1);
        var expiryCutoff = todayStart.AddDays(ExpiryWindowDays); // inclusive horizon

        // ── Pull every cross-branch aggregate ONCE, then group in memory ─────

        // Stock rows (one row per product at a branch).
        var stockRows = await _inventoryRepository.GetActiveStockRowsAsync(branchScope);
        var rowsByBranch = stockRows
            .GroupBy(r => r.Inventory.BranchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Decision logs for the scoped branches (Pending count + responsiveness).
        // One list scan, grouped by branch below.
        var decisions = await _decisionLogRepository.GetListAsync(
            d => d.BranchId.HasValue && branchScope.Contains(d.BranchId.Value));
        var decisionsByBranch = decisions
            .GroupBy(d => d.BranchId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Waste (WriteOff) cost over the last 30 days, already valued at CostPrice.
        var wasteRows = await _movementRepository.GetWriteOffAggregatesAsync(
            wasteFrom, windowEnd, branchScope);
        var wasteCostByBranch = wasteRows
            .GroupBy(r => r.BranchId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Cost));

        // Sales (denominator for the waste ratio) over the SAME 30-day window.
        var salesByBranch = (await _saleRepository.GetSalesByBranchAsync(
                wasteFrom, windowEnd, branchScope))
            .ToDictionary(s => s.BranchId, s => s.TotalAmount);

        // Batches expiring within the next few days (qty > 0, active products).
        // GetExpiringWithProductAsync returns every live batch on/before the cutoff
        // across all branches; filter to scope and group.
        var expiringBatches = await _batchRepository.GetExpiringWithProductAsync(expiryCutoff);
        var expiringByBranch = expiringBatches
            .Where(b => accessibleIds.Contains(b.Batch.BranchId))
            .GroupBy(b => b.Batch.BranchId)
            .ToDictionary(g => g.Key, g => g.Count());

        // ── Score each branch from the loaded lists ──────────────────────────
        var currency = await GetDefaultCurrencyAsync();
        var results = new List<BranchHealthDto>(branches.Count);
        foreach (var branch in branches)
        {
            var rows = rowsByBranch.TryGetValue(branch.Id, out var r) ? r : new List<InventoryStockRow>();
            var branchDecisions = decisionsByBranch.TryGetValue(branch.Id, out var d) ? d : new List<AppDecisionLog>();
            wasteCostByBranch.TryGetValue(branch.Id, out var wasteCost);
            salesByBranch.TryGetValue(branch.Id, out var salesRevenue);
            expiringByBranch.TryGetValue(branch.Id, out var expiringCount);

            var components = new List<HealthComponentDto>
            {
                ScoreStockHealth(rows),
                ScoreStockout(rows),
                ScorePendingLoad(branchDecisions),
                ScoreWasteRatio(wasteCost, salesRevenue, currency),
                ScoreExpiryRisk(expiringCount),
                ScoreResponsiveness(branchDecisions, nowUtc),
            };

            var score = components.Sum(c => c.Points);

            results.Add(new BranchHealthDto
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                Score = score,
                Grade = ToGrade(score),
                Components = components
            });
        }

        return results
            .OrderByDescending(b => b.Score)
            .ThenBy(b => b.BranchName)
            .ToList();
    }

    // ── Component scoring functions (pure, deterministic) ────────────────────

    /// <summary>
    /// 30 pts × the fraction of inventory rows that are healthy (not low, not out,
    /// not excess). Out-of-stock rows are low-stock by definition, so they already
    /// count as unhealthy here.
    /// </summary>
    private static HealthComponentDto ScoreStockHealth(List<InventoryStockRow> rows)
    {
        if (rows.Count == 0)
        {
            return Component("StockHealth", StockHealthMax, StockHealthMax,
                "No inventory tracked — assumed healthy");
        }

        var low = rows.Count(r => r.Inventory.IsLowStock);
        var excess = rows.Count(r => !r.Inventory.IsLowStock && IsExcess(r.Inventory));
        var healthy = rows.Count - low - excess;
        if (healthy < 0) healthy = 0;

        var healthyPct = (double)healthy / rows.Count;
        var points = (int)Math.Round(StockHealthMax * healthyPct, MidpointRounding.AwayFromZero);

        return Component("StockHealth", points, StockHealthMax,
            $"{Pct(healthyPct)} of {rows.Count} items healthy ({healthy} healthy, {low} low, {excess} excess)");
    }

    /// <summary>20 pts, minus 4 per out-of-stock row, floored at 0.</summary>
    private static HealthComponentDto ScoreStockout(List<InventoryStockRow> rows)
    {
        var outCount = rows.Count(r => r.Inventory.IsOutOfStock);
        var penalty = outCount * StockoutPenaltyPerRow;
        var points = Math.Max(0, StockoutMax - penalty);

        var detail = outCount == 0
            ? "No items out of stock"
            : $"{outCount} item(s) out of stock (−{penalty})";

        return Component("StockoutSeverity", points, StockoutMax, detail);
    }

    /// <summary>
    /// 15 pts. The first 3 pending decisions are free; each pending beyond the grace
    /// costs 1 pt, floored at 0.
    /// </summary>
    private static HealthComponentDto ScorePendingLoad(List<AppDecisionLog> decisions)
    {
        var pending = decisions.Count(d => d.Status == DecisionLogStatuses.Pending);
        var over = Math.Max(0, pending - PendingGrace);
        var penalty = over * PendingPenaltyPerExtra;
        var points = Math.Max(0, PendingLoadMax - penalty);

        var detail = pending == 0
            ? "No pending decisions"
            : over == 0
                ? $"{pending} pending (within grace of {PendingGrace})"
                : $"{pending} pending — {over} over grace of {PendingGrace} (−{penalty})";

        return Component("PendingLoad", points, PendingLoadMax, detail);
    }

    /// <summary>
    /// 15 pts scaled by the waste-to-sales ratio over the last 30 days:
    /// ratio = wasteCost / max(sales, 1); points = round(15 × (1 − min(ratio×10, 1))).
    /// 0% waste → 15 pts; ≥10% waste → 0 pts. No waste recorded → full 15.
    /// </summary>
    private static HealthComponentDto ScoreWasteRatio(decimal wasteCost, decimal salesRevenue, string currency)
    {
        if (wasteCost <= 0)
        {
            return Component("WasteRatio", WasteRatioMax, WasteRatioMax,
                "No waste recorded in the last 30 days");
        }

        var ratio = (double)(wasteCost / Math.Max(salesRevenue, 1m));
        var scaled = Math.Min(ratio * WasteRatioCapMultiplier, 1.0);
        var points = (int)Math.Round(WasteRatioMax * (1.0 - scaled), MidpointRounding.AwayFromZero);

        var detail = $"{Pct(ratio)} waste-to-sales over 30 days " +
                     $"({Money(wasteCost, currency)} waste vs {Money(salesRevenue, currency)} sales)";

        return Component("WasteRatio", points, WasteRatioMax, detail);
    }

    /// <summary>10 pts, minus 2 per batch expiring within 3 days, floored at 0.</summary>
    private static HealthComponentDto ScoreExpiryRisk(int expiringCount)
    {
        var penalty = expiringCount * ExpiryPenaltyPerBatch;
        var points = Math.Max(0, ExpiryRiskMax - penalty);

        var detail = expiringCount == 0
            ? $"No batches expiring within {ExpiryWindowDays} days"
            : $"{expiringCount} batch(es) expiring within {ExpiryWindowDays} days (−{penalty})";

        return Component("ExpiryRisk", points, ExpiryRiskMax, detail);
    }

    /// <summary>
    /// 10 pts × the fraction of decisions created &gt; 48h ago that have been acted on
    /// (no longer Pending). If there are no such "due" decisions, full 10.
    /// </summary>
    private static HealthComponentDto ScoreResponsiveness(List<AppDecisionLog> decisions, DateTime nowUtc)
    {
        var cutoff = nowUtc.AddHours(-ResponsivenessAgeHours);
        var due = decisions.Where(d => d.CreationTime <= cutoff).ToList();
        if (due.Count == 0)
        {
            return Component("Responsiveness", ResponsivenessMax, ResponsivenessMax,
                "No decisions older than 48h to act on");
        }

        var acted = due.Count(d => d.Status != DecisionLogStatuses.Pending);
        var actedPct = (double)acted / due.Count;
        var points = (int)Math.Round(ResponsivenessMax * actedPct, MidpointRounding.AwayFromZero);

        return Component("Responsiveness", points, ResponsivenessMax,
            $"{Pct(actedPct)} of {due.Count} decisions older than 48h acted on ({acted} resolved)");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool IsExcess(AppBranchInventory inv)
        => inv.MaximumStock.HasValue
            ? inv.QuantityOnHand > inv.MaximumStock.Value
            : inv.QuantityOnHand > ExcessStockHighThreshold;

    private static string ToGrade(int score)
        => score >= GradeACutoff ? "A"
         : score >= GradeBCutoff ? "B"
         : score >= GradeCCutoff ? "C"
         : "D";

    private static HealthComponentDto Component(string name, int points, int max, string detail)
        => new() { Name = name, Points = points, MaxPoints = max, Detail = detail };

    private static string Pct(double fraction)
        => Math.Round(fraction * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    private static string Money(decimal value, string currency)
        => $"{value.ToString("N0", CultureInfo.CurrentCulture)} {currency}";

    private async Task<string> GetDefaultCurrencyAsync()
    {
        var currency = (await _settingProvider.GetOrNullAsync("patisserie_shop.Operations.DefaultCurrency"))
            ?.Trim()
            .ToUpperInvariant();

        return currency?.Length == 3 ? currency : "USD";
    }
}
