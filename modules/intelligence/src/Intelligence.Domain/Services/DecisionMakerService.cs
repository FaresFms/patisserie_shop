using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Decisions;
using Intelligence.Entities;
using Intelligence.Rules;
using Inventory.Events;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Intelligence.Services;

public class DecisionMakerService : DomainService
{
    private readonly IRepository<AppInventoryRule, Guid> _rulesRepo;
    private readonly IRepository<AppDecisionLog, Guid> _logsRepo;
    private readonly IRepository<AppProductVelocity, Guid> _velocityRepo;

    public DecisionMakerService(
        IRepository<AppInventoryRule, Guid> rulesRepo,
        IRepository<AppDecisionLog, Guid> logsRepo,
        IRepository<AppProductVelocity, Guid> velocityRepo)
    {
        _rulesRepo = rulesRepo;
        _logsRepo = logsRepo;
        _velocityRepo = velocityRepo;
    }

    public async Task EvaluateAsync(StockChangedEto eto)
    {
        var queryable = (await _rulesRepo.GetQueryableAsync())
            .Where(r => r.IsActive
                && (r.ProductId == null || r.ProductId == eto.ProductId)
                && (r.BranchId  == null || r.BranchId  == eto.BranchId))
            .OrderByDescending(r => r.Priority);

        var rules = await AsyncExecuter.ToListAsync(queryable);

        // Tracks which decision types we've already raised in THIS evaluation so two
        // matching rules of the same kind (e.g. "Global Low Stock" + "Critical Low Stock")
        // don't both file a LowStockAlert. Rules are ordered by Priority desc, so the
        // highest-priority rule's message wins.
        var raisedTypes = new HashSet<string>();

        // DaysOfCover rules all read the same velocity row; load it lazily, once.
        var velocityLoaded = false;
        decimal avgDailySales30 = 0m;

        foreach (var rule in rules)
        {
            if (rule.RuleType == "LowStock" && rule.ThresholdValue.HasValue)
            {
                if (eto.NewQty < rule.ThresholdValue.Value)
                {
                    await TryCreateLogAsync(raisedTypes, rule, eto.ProductId, eto.BranchId, "LowStockAlert",
                        $"Stock={eto.NewQty} is below threshold={rule.ThresholdValue} (Rule: '{rule.RuleName}'). Suggested: reorder.",
                        stockAtEval: eto.NewQty);
                }
            }
            else if (rule.RuleType == "ExcessStock" && rule.ThresholdValue.HasValue)
            {
                if (eto.NewQty > rule.ThresholdValue.Value)
                {
                    await TryCreateLogAsync(raisedTypes, rule, eto.ProductId, eto.BranchId, "ExcessStockAlert",
                        $"Stock={eto.NewQty} exceeds threshold={rule.ThresholdValue} (Rule: '{rule.RuleName}'). Suggested: redistribute.",
                        stockAtEval: eto.NewQty);
                }
            }
            else if (rule.RuleType == InventoryRuleTypes.DaysOfCover && rule.ThresholdValue.HasValue)
            {
                // ThresholdValue is interpreted as days of cover for this rule type.
                // Velocity comes from the nightly AppProductVelocity read model; if no
                // row exists yet (or velocity is zero) we skip silently — dead stock
                // is the DeadStock scanner's job.
                if (!velocityLoaded)
                {
                    velocityLoaded = true;
                    var velocityQuery = (await _velocityRepo.GetQueryableAsync())
                        .Where(v => v.ProductId == eto.ProductId && v.BranchId == eto.BranchId);
                    var velocityRow = await AsyncExecuter.FirstOrDefaultAsync(velocityQuery);
                    avgDailySales30 = velocityRow?.AvgDailySales30 ?? 0m;
                }

                if (avgDailySales30 > 0)
                {
                    var daysOfCover = eto.NewQty / avgDailySales30;
                    if (daysOfCover < rule.ThresholdValue.Value)
                    {
                        await TryCreateLogAsync(raisedTypes, rule, eto.ProductId, eto.BranchId, DecisionTypes.StockoutRisk,
                            $"Stock={eto.NewQty} ÷ {avgDailySales30:0.##}/day avg (30-day) = {Math.Round(daysOfCover, 1):0.#} days of cover, " +
                            $"below the {rule.ThresholdValue}-day threshold (Rule: '{rule.RuleName}'). Suggested: reorder soon.",
                            stockAtEval: eto.NewQty);
                    }
                }
            }
            // TransferSuggestion and DeadStock are evaluated by a separate background job — not here
        }
    }

    /// <summary>
    /// Creates a decision log unless an equivalent one is already open. "Open" means a
    /// Pending log of the same DecisionType for the same product+branch already exists —
    /// either raised earlier in this same evaluation, or sitting unresolved in the store
    /// from a previous stock change. This stops the decision log from filling with
    /// duplicate alerts that reappear every time stock moves while still below threshold.
    /// </summary>
    private async Task TryCreateLogAsync(HashSet<string> raisedTypes, AppInventoryRule rule,
        Guid productId, Guid? branchId, string decisionType, string reasoning,
        int? stockAtEval = null, int? daysWithoutSale = null,
        Guid? sourceBranchId = null, Guid? targetBranchId = null)
    {
        // Already raised this type during this evaluation.
        if (!raisedTypes.Add(decisionType))
        {
            return;
        }

        // An unresolved alert of this type already exists for this product+branch.
        var openQuery = (await _logsRepo.GetQueryableAsync())
            .Where(l => l.Status == DecisionLogStatuses.Pending
                && l.ProductId == productId
                && l.BranchId == branchId
                && l.DecisionType == decisionType);

        if (await AsyncExecuter.AnyAsync(openQuery))
        {
            return;
        }

        var log = new AppDecisionLog(
            GuidGenerator.Create(),
            rule.Id,
            productId,
            branchId,
            decisionType,
            reasoning,
            rule.SuggestedAction,
            stockAtEval,
            daysWithoutSale,
            sourceBranchId,
            targetBranchId
        );
        await _logsRepo.InsertAsync(log);
    }
}
