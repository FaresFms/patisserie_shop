using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Entities;
using Intelligence.Rules;
using Inventory.Entities;
using Inventory.StockBatches;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;

namespace Intelligence.Decisions;

/// <summary>
/// Completes the intelligence loop for perishables. ExpiringSoon has no triggering
/// event (a batch "expires soon" simply because time passed), so this scanner walks
/// every live stock batch (QuantityRemaining &gt; 0) expiring inside the widest active
/// rule window, groups them per product×branch, finds the best-fit ExpiringSoon rule
/// (scope precedence + Priority) and raises a Pending <see cref="AppDecisionLog"/>
/// (DecisionType ExpiryAlert) when at least one batch expires within the rule's
/// ThresholdDays. The batch ledger is best-effort, so alerts are advisory — stock
/// truth stays with AppBranchInventory. Mirrors the rule-loading / dedup conventions
/// of DeadStockScannerService; invoked on a schedule by <c>ExpiryScannerWorker</c>.
/// </summary>
public class ExpiryScannerService : ITransientDependency
{
    private readonly IStockBatchRepository _batchRepository;
    private readonly IRepository<AppInventoryRule, Guid> _ruleRepository;
    private readonly IRepository<AppDecisionLog, Guid> _logRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly ILogger<ExpiryScannerService> _logger;

    public ExpiryScannerService(
        IStockBatchRepository batchRepository,
        IRepository<AppInventoryRule, Guid> ruleRepository,
        IRepository<AppDecisionLog, Guid> logRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IGuidGenerator guidGenerator,
        IAsyncQueryableExecuter asyncExecuter,
        ILogger<ExpiryScannerService> logger)
    {
        _batchRepository = batchRepository;
        _ruleRepository = ruleRepository;
        _logRepository = logRepository;
        _branchRepository = branchRepository;
        _guidGenerator = guidGenerator;
        _asyncExecuter = asyncExecuter;
        _logger = logger;
    }

    public async Task ScanAsync()
    {
        // Active ExpiringSoon rules, highest Priority first so RuleScopeMatcher picks
        // the winning rule inside each scope class.
        var rulesQuery = (await _ruleRepository.GetQueryableAsync())
            .Where(r => r.IsActive && r.RuleType == InventoryRuleTypes.ExpiringSoon)
            .OrderByDescending(r => r.Priority);
        var rules = await _asyncExecuter.ToListAsync(rulesQuery);

        if (rules.Count == 0)
        {
            _logger.LogInformation("ExpiryScanner: no active ExpiringSoon rules — nothing to scan.");
            return;
        }

        var maxThresholdDays = rules.Max(r => r.ThresholdDays ?? 0);
        if (maxThresholdDays <= 0)
        {
            _logger.LogInformation("ExpiryScanner: active ExpiringSoon rules have no ThresholdDays — nothing to scan.");
            return;
        }

        // Live batches (qty > 0, active products) expiring inside the widest rule
        // window — already-expired batches are included on purpose: stock that died
        // unsold is the loudest version of this alert.
        var today = DateTime.UtcNow.Date;
        var batches = await _batchRepository.GetExpiringWithProductAsync(today.AddDays(maxThresholdDays));
        if (batches.Count == 0)
        {
            _logger.LogInformation("ExpiryScanner: no live batches expiring within {Days} day(s) — nothing to scan.", maxThresholdDays);
            return;
        }

        // Branch lookup for names + active filtering (one query, no N+1).
        var branches = await _branchRepository.GetListAsync();
        var branchById = branches.ToDictionary(b => b.Id);

        // Existing Pending ExpiryAlert (Product, Branch) pairs — skip duplicates without
        // a per-row query, exactly like the other scanners.
        var pending = DecisionLogStatuses.Pending;
        var expiryType = DecisionTypes.ExpiryAlert;
        var existingQuery = (await _logRepository.GetQueryableAsync())
            .Where(l => l.DecisionType == expiryType && l.Status == pending)
            .Select(l => new { l.ProductId, l.BranchId });
        var existingRows = await _asyncExecuter.ToListAsync(existingQuery);
        var seen = new HashSet<(Guid ProductId, Guid? BranchId)>(
            existingRows.Select(x => (x.ProductId, x.BranchId)));

        var created = 0;

        foreach (var group in batches.GroupBy(b => (b.Batch.ProductId, b.Batch.BranchId)))
        {
            var (productId, branchId) = group.Key;
            var product = group.First().Product;

            // Skip inactive (or unknown) branches.
            if (!branchById.TryGetValue(branchId, out var branch) || !branch.IsActive)
            {
                continue;
            }

            var rule = RuleScopeMatcher.MatchBestRule(rules, productId, branchId);
            if (rule?.ThresholdDays is not int thresholdDays)
            {
                continue; // no governing rule for this product/branch
            }

            // Batches inside THIS rule's window (the load used the widest window).
            var windowEnd = today.AddDays(thresholdDays);
            var atRisk = group
                .Where(b => b.Batch.ExpiryDate.Date <= windowEnd)
                .OrderBy(b => b.Batch.ExpiryDate)
                .ToList();
            if (atRisk.Count == 0)
            {
                continue;
            }

            var key = (productId, (Guid?)branchId);
            if (!seen.Add(key))
            {
                continue; // already Pending (in the store or earlier in this batch)
            }

            var earliest = atRisk[0].Batch;
            var totalExpiringQty = atRisk.Sum(b => b.Batch.QuantityRemaining);

            var reasoning =
                $"Product '{product.Name}' at '{branch.Name}': {earliest.QuantityRemaining} units in batch " +
                $"{earliest.BatchNumber} {DescribeExpiry(earliest.ExpiryDate, today)}, within the " +
                $"{thresholdDays}-day threshold of rule '{rule.RuleName}'.";
            if (atRisk.Count > 1)
            {
                reasoning += $" In total {totalExpiringQty} units across {atRisk.Count} batches expire within the window.";
            }
            reasoning += " Suggest discount or transfer.";

            var log = new AppDecisionLog(
                _guidGenerator.Create(),
                ruleId: rule.Id,
                productId: productId,
                branchId: branchId,
                decisionType: DecisionTypes.ExpiryAlert,
                reasoning: reasoning,
                suggestedAction: rule.SuggestedAction,
                stockAtEvaluation: totalExpiringQty,
                daysWithoutSale: null);

            await _logRepository.InsertAsync(log, autoSave: false);
            created++;
        }

        _logger.LogInformation(
            "ExpiryScanner: created {Created} ExpiryAlert decision(s) from {Batches} expiring batch(es) against {Rules} active rule(s).",
            created, batches.Count, rules.Count);
    }

    /// <summary>"expire in 2 days (2026-06-13)" / "expire today (…)" / "expired 3 days ago (…)".</summary>
    private static string DescribeExpiry(DateTime expiryDate, DateTime today)
    {
        var days = (expiryDate.Date - today).Days;
        var dateText = expiryDate.ToString("yyyy-MM-dd");
        return days switch
        {
            < 0 => $"expired {-days} day(s) ago ({dateText})",
            0 => $"expire today ({dateText})",
            _ => $"expire in {days} day(s) ({dateText})"
        };
    }
}
