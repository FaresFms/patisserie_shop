using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Entities;
using Intelligence.Localization;
using Intelligence.Rules;
using Inventory.BranchInventory;
using Inventory.Entities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;

namespace Intelligence.Decisions;

/// <summary>
/// Scheduled scanner that suggests inter-branch transfers: for each product it finds a
/// branch that is LOW (below the matching rule's ThresholdValue) and another branch that
/// clearly has EXCESS, then raises a Pending TransferSuggestion <see cref="AppDecisionLog"/>
/// from the excess branch (source) to the low branch (target). Follows the same scope
/// matrix, Priority ordering, and Pending-dedup conventions as the other scanners; invoked
/// on a schedule by <c>TransferSuggestionScannerWorker</c>.
/// </summary>
public class TransferSuggestionScannerService : ITransientDependency
{
    private readonly IBranchInventoryRepository _inventoryRepository;
    private readonly IRepository<AppInventoryRule, Guid> _ruleRepository;
    private readonly IRepository<AppDecisionLog, Guid> _logRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IStringLocalizer<IntelligenceResource> _localizer;
    private readonly ILogger<TransferSuggestionScannerService> _logger;

    public TransferSuggestionScannerService(
        IBranchInventoryRepository inventoryRepository,
        IRepository<AppInventoryRule, Guid> ruleRepository,
        IRepository<AppDecisionLog, Guid> logRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IGuidGenerator guidGenerator,
        IAsyncQueryableExecuter asyncExecuter,
        IStringLocalizer<IntelligenceResource> localizer,
        ILogger<TransferSuggestionScannerService> logger)
    {
        _inventoryRepository = inventoryRepository;
        _ruleRepository = ruleRepository;
        _logRepository = logRepository;
        _branchRepository = branchRepository;
        _guidGenerator = guidGenerator;
        _asyncExecuter = asyncExecuter;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task ScanAsync()
    {
        using var contentCulture = PersistedContentCulture.UseArabic();

        var rulesQuery = (await _ruleRepository.GetQueryableAsync())
            .Where(r => r.IsActive && r.RuleType == InventoryRuleTypes.TransferSuggestion)
            .OrderByDescending(r => r.Priority);
        var rules = await _asyncExecuter.ToListAsync(rulesQuery);

        if (rules.Count == 0)
        {
            _logger.LogInformation("TransferSuggestionScanner: no active TransferSuggestion rules — nothing to scan.");
            return;
        }

        var rows = await _inventoryRepository.GetActiveStockRowsAsync(null);
        if (rows.Count == 0)
        {
            _logger.LogInformation("TransferSuggestionScanner: no active inventory rows — nothing to scan.");
            return;
        }

        var branches = await _branchRepository.GetListAsync();
        var branchById = branches.ToDictionary(b => b.Id);

        // Existing Pending suggestions keyed by (Product, Source, Target) so we never
        // duplicate an open suggestion for the same transfer direction.
        var pending = DecisionLogStatuses.Pending;
        var transferType = DecisionTypes.TransferSuggestion;
        var existingQuery = (await _logRepository.GetQueryableAsync())
            .Where(l => l.DecisionType == transferType && l.Status == pending)
            .Select(l => new { l.ProductId, l.SourceBranchId, l.TargetBranchId });
        var existingRows = await _asyncExecuter.ToListAsync(existingQuery);
        var seen = new HashSet<(Guid ProductId, Guid? Source, Guid? Target)>(
            existingRows.Select(x => (x.ProductId, x.SourceBranchId, x.TargetBranchId)));

        // Only consider rows whose branch is active; group by product so we can compare
        // stock levels across branches.
        var activeRows = rows
            .Where(r => branchById.TryGetValue(r.Inventory.BranchId, out var b) && b.IsActive)
            .ToList();

        var created = 0;

        foreach (var group in activeRows.GroupBy(r => r.Inventory.ProductId))
        {
            var productRows = group.ToList();
            if (productRows.Count < 2)
            {
                continue; // a product in a single branch can't be transferred anywhere
            }

            var lows = new List<(InventoryStockRow Row, AppInventoryRule Rule)>();
            var excesses = new List<InventoryStockRow>();

            foreach (var row in productRows)
            {
                var inv = row.Inventory;
                var rule = RuleScopeMatcher.MatchBestRule(rules, inv.ProductId, inv.BranchId);
                if (rule?.ThresholdValue is not int threshold)
                {
                    continue; // no governing rule for this product/branch
                }

                if (inv.QuantityOnHand < threshold)
                {
                    lows.Add((row, rule));
                }

                // Clearly too much: above the configured ceiling (Max, or Min*3 fallback)
                // OR more than triple the low threshold — whichever qualifies first.
                var ceiling = inv.MaximumStock ?? (inv.MinimumStock * 3);
                if (inv.QuantityOnHand > ceiling || inv.QuantityOnHand > threshold * 3)
                {
                    excesses.Add(row);
                }
            }

            if (lows.Count == 0 || excesses.Count == 0)
            {
                continue;
            }

            foreach (var (lowRow, rule) in lows)
            {
                var lowInv = lowRow.Inventory;
                var threshold = rule.ThresholdValue!.Value;
                var lowBranchName = branchById[lowInv.BranchId].Name;

                foreach (var excessRow in excesses)
                {
                    var excessInv = excessRow.Inventory;

                    // Never transfer to itself, and an excess branch is high by construction
                    // so it can never also be the low side (no reverse A→B / B→A pairs).
                    if (excessInv.BranchId == lowInv.BranchId)
                    {
                        continue;
                    }

                    var key = (group.Key, (Guid?)excessInv.BranchId, (Guid?)lowInv.BranchId);
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    var excessBranchName = branchById[excessInv.BranchId].Name;
                    var reasoning = _localizer[
                        "DecisionReasoning:TransferSuggestion",
                        lowRow.Product.Name,
                        lowBranchName,
                        lowInv.QuantityOnHand,
                        threshold,
                        excessBranchName,
                        excessInv.QuantityOnHand,
                        rule.RuleName];

                    var log = new AppDecisionLog(
                        _guidGenerator.Create(),
                        ruleId: rule.Id,
                        productId: group.Key,
                        branchId: lowInv.BranchId,
                        decisionType: DecisionTypes.TransferSuggestion,
                        reasoning: reasoning,
                        suggestedAction: rule.SuggestedAction,
                        stockAtEvaluation: lowInv.QuantityOnHand,
                        daysWithoutSale: null,
                        sourceBranchId: excessInv.BranchId,
                        targetBranchId: lowInv.BranchId);

                    await _logRepository.InsertAsync(log, autoSave: false);
                    created++;
                }
            }
        }

        _logger.LogInformation(
            "TransferSuggestionScanner: created {Created} TransferSuggestion decision(s) from {Rows} inventory row(s) against {Rules} active rule(s).",
            created, activeRows.Count, rules.Count);
    }
}
