using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Decisions;
using Intelligence.Entities;
using Intelligence.Rules;
using Inventory.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace Intelligence.Decisions;

/// <summary>
/// Nightly job that completes the intelligence loop. LowStock and ExcessStock
/// fire in real time via the event bus; DeadStock has no triggering event, so
/// this worker walks every branch-inventory row, matches the best-fit
/// DeadStock rule (scope priority + Priority field), and inserts a Pending
/// AppDecisionLog when the product hasn't sold within the rule's
/// ThresholdDays window.
///
/// De-duplication: a (Product, Branch) pair with an existing Pending
/// DeadStockFlag is skipped. Once that decision is acknowledged / dismissed /
/// executed, the next scan can raise a fresh one.
///
/// Interval is read from configuration at construction time:
/// "Intelligence:DeadStockScanner:IntervalSeconds" (default 86400 = 24 h).
/// Set it to 60 in appsettings.Development.json to demo the cycle.
/// </summary>
public class DeadStockScannerWorker : AsyncPeriodicBackgroundWorkerBase
{
    private const int DefaultIntervalSeconds = 86400; // 24 hours

    public DeadStockScannerWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration)
        : base(timer, serviceScopeFactory)
    {
        var intervalSeconds = configuration.GetValue<int?>("Intelligence:DeadStockScanner:IntervalSeconds")
                              ?? DefaultIntervalSeconds;

        if (intervalSeconds <= 0)
        {
            intervalSeconds = DefaultIntervalSeconds;
        }

        Timer.Period = intervalSeconds * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var sp = workerContext.ServiceProvider;
        var logger = sp.GetRequiredService<ILogger<DeadStockScannerWorker>>();
        var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
        var asyncExecuter = sp.GetRequiredService<IAsyncQueryableExecuter>();

        var biRepo       = sp.GetRequiredService<IRepository<AppBranchInventory, Guid>>();
        var ruleRepo     = sp.GetRequiredService<IRepository<AppInventoryRule, Guid>>();
        var logRepo      = sp.GetRequiredService<IRepository<AppDecisionLog, Guid>>();
        var productRepo  = sp.GetRequiredService<IRepository<AppProduct, Guid>>();
        var branchRepo   = sp.GetRequiredService<IRepository<AppBranch, Guid>>();
        var guidGenerator = sp.GetRequiredService<IGuidGenerator>();

        logger.LogInformation("DeadStockScannerWorker: scan starting.");

        using var uow = uowManager.Begin(requiresNew: true);

        // Active DeadStock rules, ordered by Priority desc so the higher-priority
        // rule wins inside each scope class.
        var rulesQuery = (await ruleRepo.GetQueryableAsync())
            .Where(r => r.IsActive && r.RuleType == InventoryRuleTypes.DeadStock)
            .OrderByDescending(r => r.Priority);
        var rules = await asyncExecuter.ToListAsync(rulesQuery);

        if (rules.Count == 0)
        {
            logger.LogInformation("DeadStockScannerWorker: no active DeadStock rules — exiting.");
            await uow.CompleteAsync();
            return;
        }

        // Every branch-inventory row that actually holds stock.
        var inventoryQuery = (await biRepo.GetQueryableAsync())
            .Where(b => b.QuantityOnHand > 0);
        var inventory = await asyncExecuter.ToListAsync(inventoryQuery);

        if (inventory.Count == 0)
        {
            logger.LogInformation("DeadStockScannerWorker: no branch-inventory rows with stock — exiting.");
            await uow.CompleteAsync();
            return;
        }

        // Lookup dictionaries — one query each, no N+1.
        var products = await productRepo.GetListAsync();
        var productById = products.ToDictionary(p => p.Id);
        var branches = await branchRepo.GetListAsync();
        var branchById = branches.ToDictionary(b => b.Id);

        // Existing PENDING DeadStockFlag logs — used to skip duplicates without
        // a per-row query. Resolved logs (Acknowledged/Dismissed/Executed) are
        // NOT in this set, so a new scan can raise a fresh decision after
        // someone acted on the previous one.
        var pendingKey = DecisionLogStatuses.Pending;
        var deadType = DecisionTypes.DeadStockFlag;
        var existingQuery = (await logRepo.GetQueryableAsync())
            .Where(l => l.DecisionType == deadType && l.Status == pendingKey)
            .Select(l => new { l.ProductId, l.BranchId });
        var existingRows = await asyncExecuter.ToListAsync(existingQuery);
        var existing = new HashSet<(Guid ProductId, Guid? BranchId)>(
            existingRows.Select(x => (x.ProductId, x.BranchId)));

        var today = DateTime.UtcNow.Date;
        var created = 0;

        foreach (var bi in inventory)
        {
            var rule = MatchBestRule(rules, bi.ProductId, bi.BranchId);
            if (rule == null || !rule.ThresholdDays.HasValue) continue;

            int? daysWithoutSale;
            bool exceeded;

            if (bi.LastSoldDate.HasValue)
            {
                var days = (int)Math.Floor((today - bi.LastSoldDate.Value.Date).TotalDays);
                exceeded = days > rule.ThresholdDays.Value;
                daysWithoutSale = exceeded ? days : (int?)null;
            }
            else
            {
                // Never sold — by definition exceeds any threshold. The DTO
                // records "no value" so the dashboard renders "never sold".
                exceeded = true;
                daysWithoutSale = null;
            }

            if (!exceeded) continue;

            var key = (bi.ProductId, (Guid?)bi.BranchId);
            if (existing.Contains(key)) continue;

            var productName = productById.TryGetValue(bi.ProductId, out var p) ? p.Name : "(unknown product)";
            var branchName  = branchById.TryGetValue(bi.BranchId,  out var b) ? b.Name : "(unknown branch)";

            var daysText = daysWithoutSale.HasValue
                ? $"{daysWithoutSale.Value} days"
                : "an unknown period (never sold)";

            var reasoning = $"Product '{productName}' at '{branchName}' has not sold for {daysText} " +
                            $"(threshold: {rule.ThresholdDays} days). Rule '{rule.RuleName}' fired.";

            var log = new AppDecisionLog(
                guidGenerator.Create(),
                ruleId: rule.Id,
                productId: bi.ProductId,
                branchId: bi.BranchId,
                decisionType: DecisionTypes.DeadStockFlag,
                reasoning: reasoning,
                suggestedAction: rule.SuggestedAction,
                stockAtEvaluation: bi.QuantityOnHand,
                daysWithoutSale: daysWithoutSale);

            await logRepo.InsertAsync(log, autoSave: false);

            // Track in-memory too so multiple inventory rows for the same
            // (product, branch) pair in this batch can't create duplicates.
            existing.Add(key);
            created++;
        }

        await uow.CompleteAsync();

        logger.LogInformation(
            "DeadStockScannerWorker: scan complete — created {Count} DeadStockFlag decision log(s) across {Rules} active rule(s) and {Rows} inventory row(s).",
            created, rules.Count, inventory.Count);
    }

    /// <summary>
    /// Returns the best-fit rule for a (productId, branchId) pair using the
    /// scope priority described in the spec, with Priority desc breaking ties
    /// inside each scope class (the rules list is already pre-sorted).
    /// </summary>
    private static AppInventoryRule? MatchBestRule(
        IReadOnlyList<AppInventoryRule> rulesByPriorityDesc,
        Guid productId,
        Guid branchId)
    {
        // 1) Exact match: this product in this branch.
        var rule = rulesByPriorityDesc.FirstOrDefault(r =>
            r.ProductId == productId && r.BranchId == branchId);
        if (rule != null) return rule;

        // 2) Product-only: this product in all branches.
        rule = rulesByPriorityDesc.FirstOrDefault(r =>
            r.ProductId == productId && r.BranchId == null);
        if (rule != null) return rule;

        // 3) Branch-only: all products in this branch.
        rule = rulesByPriorityDesc.FirstOrDefault(r =>
            r.ProductId == null && r.BranchId == branchId);
        if (rule != null) return rule;

        // 4) Global: all products in all branches.
        return rulesByPriorityDesc.FirstOrDefault(r =>
            r.ProductId == null && r.BranchId == null);
    }
}
