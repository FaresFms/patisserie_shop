using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Entities;
using Intelligence.Rules;
using Inventory.BranchInventory;
using Inventory.Entities;
using Microsoft.Extensions.Logging;
using Operations.Sales;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;

namespace Intelligence.Decisions;

/// <summary>
/// Nightly demand-velocity computation + StockoutRisk sweep, in that order:
///
/// 1. Pulls per product×branch sale aggregates for the trailing 7/30-day windows and
///    UPSERTS the <see cref="AppProductVelocity"/> read model (rows whose product×branch
///    had no sales in 30 days are zeroed, not deleted, so days-of-cover math sees zero
///    velocity). ABC classification is per PRODUCT globally: 30-day revenue summed
///    across branches, sorted descending, cumulative share ≤80% → "A", ≤95% → "B",
///    else "C" (zero revenue → "C"); the product's class is written onto all its
///    branch rows.
/// 2. Sweeps every active stock row against the active DaysOfCover rules
///    (ThresholdValue = days of cover) and raises a Pending StockoutRisk
///    <see cref="AppDecisionLog"/> when QuantityOnHand ÷ AvgDailySales30 falls below
///    the matched rule's threshold. Zero-velocity rows are skipped — dead stock is the
///    DeadStock scanner's job. Follows the same scope matrix, Priority ordering and
///    Pending-dedup conventions as the other scanners; invoked on a schedule by
///    <c>VelocityScannerWorker</c>.
/// </summary>
public class VelocityScannerService : ITransientDependency
{
    private readonly ISaleRepository _saleRepository;
    private readonly IRepository<AppProductVelocity, Guid> _velocityRepository;
    private readonly IBranchInventoryRepository _inventoryRepository;
    private readonly IRepository<AppInventoryRule, Guid> _ruleRepository;
    private readonly IRepository<AppDecisionLog, Guid> _logRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly ILogger<VelocityScannerService> _logger;

    public VelocityScannerService(
        ISaleRepository saleRepository,
        IRepository<AppProductVelocity, Guid> velocityRepository,
        IBranchInventoryRepository inventoryRepository,
        IRepository<AppInventoryRule, Guid> ruleRepository,
        IRepository<AppDecisionLog, Guid> logRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IGuidGenerator guidGenerator,
        IAsyncQueryableExecuter asyncExecuter,
        ILogger<VelocityScannerService> logger)
    {
        _saleRepository = saleRepository;
        _velocityRepository = velocityRepository;
        _inventoryRepository = inventoryRepository;
        _ruleRepository = ruleRepository;
        _logRepository = logRepository;
        _branchRepository = branchRepository;
        _guidGenerator = guidGenerator;
        _asyncExecuter = asyncExecuter;
        _logger = logger;
    }

    public async Task ScanAsync()
    {
        var nowUtc = DateTime.UtcNow;

        // Velocity computation MUST run before the sweep so days-of-cover math
        // uses tonight's numbers, not yesterday's.
        var velocityByKey = await ComputeVelocitiesAsync(nowUtc);
        await SweepStockoutRiskAsync(velocityByKey);
    }

    /// <summary>
    /// Rebuilds the velocity read model and returns the upserted rows keyed by
    /// (ProductId, BranchId) so the sweep can read tonight's values in memory.
    /// </summary>
    private async Task<Dictionary<(Guid ProductId, Guid BranchId), AppProductVelocity>> ComputeVelocitiesAsync(
        DateTime nowUtc)
    {
        var from7Utc = nowUtc.AddDays(-7);
        var from30Utc = nowUtc.AddDays(-30);

        var aggregates = await _saleRepository.GetProductBranchSalesAggregatesAsync(
            from7Utc, from30Utc, nowUtc);

        var abcByProduct = ComputeAbcClasses(aggregates);

        var existingRows = await _velocityRepository.GetListAsync();
        var rowByKey = existingRows.ToDictionary(v => (v.ProductId, v.BranchId));

        var inserted = 0;
        var updated = 0;
        var zeroed = 0;
        var seen = new HashSet<(Guid ProductId, Guid BranchId)>();

        foreach (var agg in aggregates)
        {
            seen.Add((agg.ProductId, agg.BranchId));

            var avgDaily7 = agg.QuantitySold7 / 7m;
            var avgDaily30 = agg.QuantitySold30 / 30m;
            var abcClass = abcByProduct.GetValueOrDefault(agg.ProductId, "C");

            if (rowByKey.TryGetValue((agg.ProductId, agg.BranchId), out var row))
            {
                row.UpdateMetrics(avgDaily7, avgDaily30, agg.QuantitySold30, agg.Revenue30, abcClass, nowUtc);
                await _velocityRepository.UpdateAsync(row, autoSave: false);
                updated++;
            }
            else
            {
                row = new AppProductVelocity(_guidGenerator.Create(), agg.ProductId, agg.BranchId);
                row.UpdateMetrics(avgDaily7, avgDaily30, agg.QuantitySold30, agg.Revenue30, abcClass, nowUtc);
                await _velocityRepository.InsertAsync(row, autoSave: false);
                rowByKey[(agg.ProductId, agg.BranchId)] = row;
                inserted++;
            }
        }

        // Product×branch pairs with no sales in 30 days: zero the metrics but KEEP the
        // row so days-of-cover math (and the UI) sees an explicit zero velocity. The
        // product may still be class A/B globally through its other branches.
        foreach (var row in existingRows)
        {
            if (seen.Contains((row.ProductId, row.BranchId)))
            {
                continue;
            }

            var abcClass = abcByProduct.GetValueOrDefault(row.ProductId, "C");
            row.UpdateMetrics(0m, 0m, 0, 0m, abcClass, nowUtc);
            await _velocityRepository.UpdateAsync(row, autoSave: false);
            zeroed++;
        }

        _logger.LogInformation(
            "VelocityScanner: computed velocity for {Aggregates} product×branch aggregate(s) — {Inserted} inserted, {Updated} updated, {Zeroed} zeroed (no sales in 30 days).",
            aggregates.Count, inserted, updated, zeroed);

        return rowByKey;
    }

    /// <summary>
    /// ABC classification per product, globally: sum Revenue30 across branches, sort
    /// descending, walk the cumulative revenue share — ≤80% → "A", ≤95% → "B",
    /// else "C". Products with zero revenue are always "C".
    /// </summary>
    private static Dictionary<Guid, string> ComputeAbcClasses(
        IReadOnlyCollection<ProductBranchSalesAggregate> aggregates)
    {
        var revenueByProduct = new Dictionary<Guid, decimal>();
        foreach (var agg in aggregates)
        {
            revenueByProduct[agg.ProductId] =
                revenueByProduct.GetValueOrDefault(agg.ProductId) + agg.Revenue30;
        }

        var result = new Dictionary<Guid, string>(revenueByProduct.Count);
        var totalRevenue = revenueByProduct.Values.Sum();

        if (totalRevenue <= 0)
        {
            foreach (var productId in revenueByProduct.Keys)
            {
                result[productId] = "C";
            }
            return result;
        }

        var cumulative = 0m;
        foreach (var pair in revenueByProduct.OrderByDescending(p => p.Value))
        {
            if (pair.Value <= 0)
            {
                result[pair.Key] = "C";
                continue;
            }

            cumulative += pair.Value;
            var share = cumulative / totalRevenue;
            result[pair.Key] = share <= 0.80m ? "A" : share <= 0.95m ? "B" : "C";
        }

        return result;
    }

    private async Task SweepStockoutRiskAsync(
        IReadOnlyDictionary<(Guid ProductId, Guid BranchId), AppProductVelocity> velocityByKey)
    {
        // Active DaysOfCover rules, highest Priority first so RuleScopeMatcher picks
        // the winning rule inside each scope class.
        var rulesQuery = (await _ruleRepository.GetQueryableAsync())
            .Where(r => r.IsActive && r.RuleType == InventoryRuleTypes.DaysOfCover)
            .OrderByDescending(r => r.Priority);
        var rules = await _asyncExecuter.ToListAsync(rulesQuery);

        if (rules.Count == 0)
        {
            _logger.LogInformation("VelocityScanner: no active DaysOfCover rules — StockoutRisk sweep skipped.");
            return;
        }

        var rows = await _inventoryRepository.GetActiveStockRowsAsync(null);
        if (rows.Count == 0)
        {
            _logger.LogInformation("VelocityScanner: no active inventory rows — StockoutRisk sweep skipped.");
            return;
        }

        // Branch lookup for names + active filtering (one query, no N+1).
        var branches = await _branchRepository.GetListAsync();
        var branchById = branches.ToDictionary(b => b.Id);

        // Existing Pending StockoutRisk (Product, Branch) pairs — skip duplicates
        // without a per-row query, exactly like the other scanners.
        var pending = DecisionLogStatuses.Pending;
        var stockoutType = DecisionTypes.StockoutRisk;
        var existingQuery = (await _logRepository.GetQueryableAsync())
            .Where(l => l.DecisionType == stockoutType && l.Status == pending)
            .Select(l => new { l.ProductId, l.BranchId });
        var existingRows = await _asyncExecuter.ToListAsync(existingQuery);
        var seen = new HashSet<(Guid ProductId, Guid? BranchId)>(
            existingRows.Select(x => (x.ProductId, x.BranchId)));

        var created = 0;

        foreach (var row in rows)
        {
            var inventory = row.Inventory;
            var product = row.Product;

            // Skip inactive (or unknown) branches.
            if (!branchById.TryGetValue(inventory.BranchId, out var branch) || !branch.IsActive)
            {
                continue;
            }

            var rule = RuleScopeMatcher.MatchBestRule(rules, inventory.ProductId, inventory.BranchId);
            if (rule?.ThresholdValue is not int thresholdDays)
            {
                continue; // no governing rule for this product/branch
            }

            if (!velocityByKey.TryGetValue((inventory.ProductId, inventory.BranchId), out var velocity)
                || velocity.AvgDailySales30 <= 0)
            {
                continue; // no demand → dead stock is another scanner's job
            }

            var daysOfCover = inventory.QuantityOnHand / velocity.AvgDailySales30;
            if (daysOfCover >= thresholdDays)
            {
                continue;
            }

            var key = (inventory.ProductId, (Guid?)inventory.BranchId);
            if (!seen.Add(key))
            {
                continue; // already Pending (in the store or earlier in this batch)
            }

            var reasoning =
                $"Product '{product.Name}' at '{branch.Name}': {inventory.QuantityOnHand} on hand ÷ " +
                $"{velocity.AvgDailySales30:0.##}/day avg (30-day) = {Math.Round(daysOfCover, 1):0.#} days of cover, " +
                $"below the {thresholdDays}-day threshold of rule '{rule.RuleName}'. ABC class {velocity.AbcClass}.";

            var log = new AppDecisionLog(
                _guidGenerator.Create(),
                ruleId: rule.Id,
                productId: inventory.ProductId,
                branchId: inventory.BranchId,
                decisionType: DecisionTypes.StockoutRisk,
                reasoning: reasoning,
                suggestedAction: rule.SuggestedAction,
                stockAtEvaluation: inventory.QuantityOnHand,
                daysWithoutSale: null);

            await _logRepository.InsertAsync(log, autoSave: false);
            created++;
        }

        _logger.LogInformation(
            "VelocityScanner: created {Created} StockoutRisk decision(s) from {Rows} inventory row(s) against {Rules} active rule(s).",
            created, rows.Count, rules.Count);
    }
}
