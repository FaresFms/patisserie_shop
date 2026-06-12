using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Entities;
using Inventory.BranchInventory;
using Inventory.Entities;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace Intelligence.Decisions;

/// <summary>
/// Closes the deterministic learning loop: ~48 hours after a decision is created this
/// scanner looks at the CURRENT stock situation and records whether the underlying
/// problem actually got resolved (<see cref="DecisionOutcomes"/>). The outcome feeds the
/// rule-effectiveness statistics on the Inventory Rules page (noisy rules, dismissed
/// alerts that ended in stockouts, effective rules).
///
/// AppDecisionLog rows are otherwise immutable, but <c>RecordOutcome</c> is the second
/// (and last) sanctioned mutation of the ledger — same controlled-mutation pattern as
/// the single Pending→Acknowledged/Dismissed/Executed transition. Mirrors the
/// scanner/worker conventions of DeadStockScannerService; invoked on a schedule by
/// <c>DecisionOutcomeScannerWorker</c>.
/// </summary>
public class DecisionOutcomeScannerService : ITransientDependency
{
    /// <summary>Decisions younger than this are not evaluated yet — give the manager time to act.</summary>
    private const int EvaluationDelayHours = 48;

    /// <summary>Upper bound per run so a large backlog cannot blow up a single unit of work.</summary>
    private const int MaxBatchSize = 500;

    private readonly IRepository<AppDecisionLog, Guid> _logRepository;
    private readonly IRepository<AppInventoryRule, Guid> _ruleRepository;
    private readonly IRepository<AppProductVelocity, Guid> _velocityRepository;
    private readonly IBranchInventoryRepository _inventoryRepository;
    private readonly IRepository<AppStockBatch, Guid> _batchRepository;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly ILogger<DecisionOutcomeScannerService> _logger;

    public DecisionOutcomeScannerService(
        IRepository<AppDecisionLog, Guid> logRepository,
        IRepository<AppInventoryRule, Guid> ruleRepository,
        IRepository<AppProductVelocity, Guid> velocityRepository,
        IBranchInventoryRepository inventoryRepository,
        IRepository<AppStockBatch, Guid> batchRepository,
        IAsyncQueryableExecuter asyncExecuter,
        ILogger<DecisionOutcomeScannerService> logger)
    {
        _logRepository = logRepository;
        _ruleRepository = ruleRepository;
        _velocityRepository = velocityRepository;
        _inventoryRepository = inventoryRepository;
        _batchRepository = batchRepository;
        _asyncExecuter = asyncExecuter;
        _logger = logger;
    }

    public async Task ScanAsync()
    {
        var nowUtc = DateTime.UtcNow;
        var cutoffUtc = nowUtc.AddHours(-EvaluationDelayHours);

        // Unevaluated decisions old enough to judge, oldest first, capped per run.
        var dueQuery = (await _logRepository.GetQueryableAsync())
            .Where(l => l.Outcome == null && l.CreationTime < cutoffUtc)
            .OrderBy(l => l.CreationTime)
            .Take(MaxBatchSize);
        var decisions = await _asyncExecuter.ToListAsync(dueQuery);

        if (decisions.Count == 0)
        {
            _logger.LogInformation("DecisionOutcomeScanner: no decisions due for outcome evaluation.");
            return;
        }

        if (decisions.Count == MaxBatchSize)
        {
            _logger.LogInformation(
                "DecisionOutcomeScanner: batch capped at {Cap} decisions — the remainder will be evaluated on the next run.",
                MaxBatchSize);
        }

        // Current stock per (Product, Branch), loaded once. GetActiveStockRowsAsync only
        // returns rows whose product is still active — a missing row therefore means the
        // product was deactivated/removed and the decision is judged Unresolved.
        var stockRows = await _inventoryRepository.GetActiveStockRowsAsync(null);
        var stockByKey = stockRows.ToDictionary(
            r => (r.Inventory.ProductId, r.Inventory.BranchId),
            r => r.Inventory);

        // Rules referenced by this batch. AppInventoryRule is soft-deleted, so deleted
        // rules simply don't come back — every branch below tolerates a missing rule.
        var ruleIds = decisions.Select(d => d.RuleId).Distinct().ToList();
        var rules = await _ruleRepository.GetListAsync(r => ruleIds.Contains(r.Id));
        var ruleById = rules.ToDictionary(r => r.Id);

        // Velocity rows are only needed to judge StockoutRisk decisions (days-of-cover math).
        var velocityByKey = new Dictionary<(Guid ProductId, Guid BranchId), AppProductVelocity>();
        if (decisions.Any(d => d.DecisionType == DecisionTypes.StockoutRisk))
        {
            var velocities = await _velocityRepository.GetListAsync();
            velocityByKey = velocities.ToDictionary(v => (v.ProductId, v.BranchId));
        }

        // Live batch expiry dates are only needed to judge ExpiryAlert and
        // WasteWriteOff decisions.
        var batchExpiriesByKey = new Dictionary<(Guid ProductId, Guid BranchId), List<DateTime>>();
        if (decisions.Any(d => d.DecisionType == DecisionTypes.ExpiryAlert
                               || d.DecisionType == DecisionTypes.WasteWriteOff))
        {
            var liveBatches = await _batchRepository.GetListAsync(b => b.QuantityRemaining > 0);
            batchExpiriesByKey = liveBatches
                .GroupBy(b => (b.ProductId, b.BranchId))
                .ToDictionary(g => g.Key, g => g.Select(b => b.ExpiryDate).ToList());
        }

        var resolved = 0;
        var stockedOut = 0;
        var unresolved = 0;

        foreach (var decision in decisions)
        {
            var outcome = EvaluateOutcome(decision, stockByKey, ruleById, velocityByKey, batchExpiriesByKey, nowUtc.Date);

            // Sanctioned controlled mutation of the otherwise-immutable ledger row
            // (see RecordOutcome). Persisted in one batch when the worker's unit of
            // work completes — no per-row autoSave.
            decision.RecordOutcome(outcome, nowUtc);
            await _logRepository.UpdateAsync(decision, autoSave: false);

            switch (outcome)
            {
                case DecisionOutcomes.Resolved: resolved++; break;
                case DecisionOutcomes.StockedOut: stockedOut++; break;
                default: unresolved++; break;
            }
        }

        _logger.LogInformation(
            "DecisionOutcomeScanner: evaluated {Evaluated} decision(s) — {Resolved} resolved, {StockedOut} stocked out, {Unresolved} unresolved.",
            decisions.Count, resolved, stockedOut, unresolved);
    }

    /// <summary>
    /// The deterministic outcome table — every branch is a single explainable rule.
    /// </summary>
    private static string EvaluateOutcome(
        AppDecisionLog decision,
        IReadOnlyDictionary<(Guid ProductId, Guid BranchId), AppBranchInventory> stockByKey,
        IReadOnlyDictionary<Guid, AppInventoryRule> ruleById,
        IReadOnlyDictionary<(Guid ProductId, Guid BranchId), AppProductVelocity> velocityByKey,
        IReadOnlyDictionary<(Guid ProductId, Guid BranchId), List<DateTime>> batchExpiriesByKey,
        DateTime todayUtc)
    {
        // TransferSuggestion is judged at the branch that needed the stock (target);
        // every other type at the branch the decision was raised for.
        var branchId = decision.DecisionType == DecisionTypes.TransferSuggestion
            ? decision.TargetBranchId ?? decision.BranchId
            : decision.BranchId;

        // No branch, or inventory row missing (product deactivated/removed) → Unresolved.
        if (branchId == null || !stockByKey.TryGetValue((decision.ProductId, branchId.Value), out var inventory))
        {
            return DecisionOutcomes.Unresolved;
        }

        var qty = inventory.QuantityOnHand;
        ruleById.TryGetValue(decision.RuleId, out var rule);
        var threshold = rule?.ThresholdValue;

        switch (decision.DecisionType)
        {
            case DecisionTypes.LowStockAlert:
            case DecisionTypes.ReorderSuggestion:
                // Hit zero → worst case; recovered above the rule threshold → resolved; else still low.
                if (qty == 0) return DecisionOutcomes.StockedOut;
                // Rule deleted (or threshold-less): can't judge "recovered", so only zero counts as bad.
                if (threshold.HasValue && qty > threshold.Value) return DecisionOutcomes.Resolved;
                return DecisionOutcomes.Unresolved;

            case DecisionTypes.StockoutRisk:
                // The predicted stockout actually happened.
                if (qty == 0) return DecisionOutcomes.StockedOut;
                // Rule deleted: can't recompute the days-of-cover threshold → unresolved.
                if (!threshold.HasValue) return DecisionOutcomes.Unresolved;
                // Velocity gone or zero: the demand evaporated, so the risk did too.
                if (!velocityByKey.TryGetValue((decision.ProductId, branchId.Value), out var velocity)
                    || velocity.AvgDailySales30 <= 0)
                {
                    return DecisionOutcomes.Resolved;
                }
                // Days of cover climbed back above the rule's threshold → risk averted.
                return qty / velocity.AvgDailySales30 > threshold.Value
                    ? DecisionOutcomes.Resolved
                    : DecisionOutcomes.Unresolved;

            case DecisionTypes.TransferSuggestion:
                // The target branch ran completely dry → worst case.
                if (qty == 0) return DecisionOutcomes.StockedOut;
                // Target branch recovered above the rule threshold → resolved (however it got there).
                if (threshold.HasValue && qty > threshold.Value) return DecisionOutcomes.Resolved;
                return DecisionOutcomes.Unresolved;

            case DecisionTypes.ExcessStockAlert:
                // For excess, zero stock means the excess is definitively gone → resolved.
                if (qty == 0) return DecisionOutcomes.Resolved;
                // Stock fell back to/under the rule's ceiling → resolved; else still in excess.
                if (threshold.HasValue && qty <= threshold.Value) return DecisionOutcomes.Resolved;
                return DecisionOutcomes.Unresolved;

            case DecisionTypes.DeadStockFlag:
                // It sold again after the decision was raised → no longer dead.
                return inventory.LastSoldDate.HasValue && inventory.LastSoldDate.Value > decision.CreationTime
                    ? DecisionOutcomes.Resolved
                    : DecisionOutcomes.Unresolved;

            case DecisionTypes.ExpiryAlert:
            {
                // Rule deleted (or threshold-less): can't recompute the expiry window → unresolved.
                if (rule?.ThresholdDays is not int windowDays)
                {
                    return DecisionOutcomes.Unresolved;
                }
                // Resolved when nothing at risk remains: no live batch (qty > 0) that is
                // already expired or expiring within the rule's window — i.e. the at-risk
                // stock was sold/moved before dying. An expired batch always satisfies
                // ExpiryDate <= windowEnd, so one comparison covers both conditions.
                var windowEnd = todayUtc.AddDays(windowDays);
                var stillAtRisk = batchExpiriesByKey.TryGetValue((decision.ProductId, branchId.Value), out var expiries)
                    && expiries.Any(d => d.Date <= windowEnd);
                return stillAtRisk ? DecisionOutcomes.Unresolved : DecisionOutcomes.Resolved;
            }

            case DecisionTypes.WasteWriteOff:
            {
                // Resolved when no expired live batch (qty > 0, ExpiryDate strictly
                // before today) remains for this product+branch — i.e. the expired
                // stock was actually written off (or otherwise cleared from the
                // ledger). Anything still sitting there expired is Unresolved.
                var stillExpired = batchExpiriesByKey.TryGetValue((decision.ProductId, branchId.Value), out var liveExpiries)
                    && liveExpiries.Any(d => d.Date < todayUtc);
                return stillExpired ? DecisionOutcomes.Unresolved : DecisionOutcomes.Resolved;
            }

            default:
                // Unknown/future decision type: record Unresolved rather than re-scanning forever.
                return DecisionOutcomes.Unresolved;
        }
    }
}
