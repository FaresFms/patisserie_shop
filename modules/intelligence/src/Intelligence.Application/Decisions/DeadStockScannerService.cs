using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Entities;
using Intelligence.Rules;
using Inventory.BranchInventory;
using Inventory.Entities;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;

namespace Intelligence.Decisions;

/// <summary>
/// Completes the intelligence loop for DeadStock. LowStock / ExcessStock fire in real
/// time off StockChangedEto via <c>DecisionMakerService</c>; DeadStock has no triggering
/// event, so this scanner walks every active branch-inventory row, finds the best-fit
/// DeadStock rule (scope precedence + Priority), and raises a Pending
/// <see cref="AppDecisionLog"/> when the product hasn't sold within the rule's
/// ThresholdDays window. Mirrors the rule-loading / dedup conventions of
/// DecisionMakerService; invoked on a schedule by <c>DeadStockScannerWorker</c>.
/// </summary>
public class DeadStockScannerService : ITransientDependency
{
    private readonly IBranchInventoryRepository _inventoryRepository;
    private readonly IRepository<AppInventoryRule, Guid> _ruleRepository;
    private readonly IRepository<AppDecisionLog, Guid> _logRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly ILogger<DeadStockScannerService> _logger;

    public DeadStockScannerService(
        IBranchInventoryRepository inventoryRepository,
        IRepository<AppInventoryRule, Guid> ruleRepository,
        IRepository<AppDecisionLog, Guid> logRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IGuidGenerator guidGenerator,
        IAsyncQueryableExecuter asyncExecuter,
        ILogger<DeadStockScannerService> logger)
    {
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
        // Active DeadStock rules, highest Priority first so RuleScopeMatcher picks the
        // winning rule inside each scope class.
        var rulesQuery = (await _ruleRepository.GetQueryableAsync())
            .Where(r => r.IsActive && r.RuleType == InventoryRuleTypes.DeadStock)
            .OrderByDescending(r => r.Priority);
        var rules = await _asyncExecuter.ToListAsync(rulesQuery);

        if (rules.Count == 0)
        {
            _logger.LogInformation("DeadStockScanner: no active DeadStock rules — nothing to scan.");
            return;
        }

        // Active-product inventory rows joined with their product. GetActiveStockRowsAsync
        // already filters out inactive products and does NOT filter on quantity, so
        // zero-stock rows are still evaluated (a product sitting unsold is the whole point).
        var rows = await _inventoryRepository.GetActiveStockRowsAsync(null);
        if (rows.Count == 0)
        {
            _logger.LogInformation("DeadStockScanner: no active inventory rows — nothing to scan.");
            return;
        }

        // Branch lookup for names + active filtering (one query, no N+1).
        var branches = await _branchRepository.GetListAsync();
        var branchById = branches.ToDictionary(b => b.Id);

        // Existing Pending DeadStockFlag (Product, Branch) pairs — skip duplicates without
        // a per-row query. Resolved logs are excluded, so a fresh scan can re-raise after
        // someone acts on the previous decision.
        var pending = DecisionLogStatuses.Pending;
        var deadType = DecisionTypes.DeadStockFlag;
        var existingQuery = (await _logRepository.GetQueryableAsync())
            .Where(l => l.DecisionType == deadType && l.Status == pending)
            .Select(l => new { l.ProductId, l.BranchId });
        var existingRows = await _asyncExecuter.ToListAsync(existingQuery);
        var seen = new HashSet<(Guid ProductId, Guid? BranchId)>(
            existingRows.Select(x => (x.ProductId, x.BranchId)));

        var today = DateTime.UtcNow.Date;
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
            if (rule?.ThresholdDays is not int thresholdDays)
            {
                continue;
            }

            // Never sold → measure from when the row was created; otherwise from LastSoldDate.
            var lastActivity = inventory.LastSoldDate?.Date ?? inventory.CreationTime.Date;
            var daysSinceLastSale = Math.Max(0, (int)(today - lastActivity).TotalDays);

            if (daysSinceLastSale < thresholdDays)
            {
                continue;
            }

            var key = (inventory.ProductId, (Guid?)inventory.BranchId);
            if (!seen.Add(key))
            {
                continue; // already Pending (in the store or earlier in this batch)
            }

            var reasoning =
                $"Product '{product.Name}' at '{branch.Name}' has not been sold for {daysSinceLastSale} days " +
                $"(threshold: {thresholdDays} days). Rule '{rule.RuleName}' fired.";

            var log = new AppDecisionLog(
                _guidGenerator.Create(),
                ruleId: rule.Id,
                productId: inventory.ProductId,
                branchId: inventory.BranchId,
                decisionType: DecisionTypes.DeadStockFlag,
                reasoning: reasoning,
                suggestedAction: rule.SuggestedAction,
                stockAtEvaluation: inventory.QuantityOnHand,
                daysWithoutSale: daysSinceLastSale);

            await _logRepository.InsertAsync(log, autoSave: false);
            created++;
        }

        _logger.LogInformation(
            "DeadStockScanner: created {Created} DeadStockFlag decision(s) from {Rows} inventory row(s) against {Rules} active rule(s).",
            created, rows.Count, rules.Count);
    }
}
