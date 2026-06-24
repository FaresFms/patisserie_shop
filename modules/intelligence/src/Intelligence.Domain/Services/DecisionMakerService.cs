using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Decisions;
using Intelligence.Entities;
using Intelligence.Localization;
using Intelligence.Rules;
using Intelligence.Velocity;
using Inventory.Events;
using Microsoft.Extensions.Localization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Intelligence.Services;

public class DecisionMakerService : DomainService
{
    /// <summary>
    /// Horizon of the StockoutRisk forecast walk — mirrors the nightly
    /// VelocityScannerService sweep so real-time and batch paths agree.
    /// </summary>
    private const int ForecastHorizonDays = 30;

    private readonly IRepository<AppInventoryRule, Guid> _rulesRepo;
    private readonly IRepository<AppDecisionLog, Guid> _logsRepo;
    private readonly IRepository<AppProductVelocity, Guid> _velocityRepo;
    private readonly IStringLocalizer<IntelligenceResource> _localizer;

    public DecisionMakerService(
        IRepository<AppInventoryRule, Guid> rulesRepo,
        IRepository<AppDecisionLog, Guid> logsRepo,
        IRepository<AppProductVelocity, Guid> velocityRepo,
        IStringLocalizer<IntelligenceResource> localizer)
    {
        _rulesRepo = rulesRepo;
        _logsRepo = logsRepo;
        _velocityRepo = velocityRepo;
        _localizer = localizer;
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
        AppProductVelocity? velocity = null;

        foreach (var rule in rules)
        {
            if (rule.RuleType == "LowStock" && rule.ThresholdValue.HasValue)
            {
                if (eto.NewQty < rule.ThresholdValue.Value)
                {
                    await TryCreateLogAsync(raisedTypes, rule, eto.ProductId, eto.BranchId, "LowStockAlert",
                        _localizer["DecisionReasoning:LowStock", eto.NewQty, rule.ThresholdValue, rule.RuleName],
                        stockAtEval: eto.NewQty);
                }
            }
            else if (rule.RuleType == "ExcessStock" && rule.ThresholdValue.HasValue)
            {
                if (eto.NewQty > rule.ThresholdValue.Value)
                {
                    await TryCreateLogAsync(raisedTypes, rule, eto.ProductId, eto.BranchId, "ExcessStockAlert",
                        _localizer["DecisionReasoning:ExcessStock", eto.NewQty, rule.ThresholdValue, rule.RuleName],
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
                    velocity = await AsyncExecuter.FirstOrDefaultAsync(velocityQuery);
                }

                if (velocity is { AvgDailySales30: > 0 })
                {
                    var avgDailySales30 = velocity.AvgDailySales30;
                    var indices = velocity.GetWeekdayIndices();

                    if (ForecastWalker.IsFlat(indices))
                    {
                        // No weekday pattern → plain division, exactly as before.
                        var daysOfCover = eto.NewQty / avgDailySales30;
                        if (daysOfCover < rule.ThresholdValue.Value)
                        {
                            await TryCreateLogAsync(raisedTypes, rule, eto.ProductId, eto.BranchId, DecisionTypes.StockoutRisk,
                                _localizer[
                                    "DecisionReasoning:StockoutRisk:Flat",
                                    eto.NewQty,
                                    Math.Round(daysOfCover, 1),
                                    rule.ThresholdValue,
                                    rule.RuleName],
                                stockAtEval: eto.NewQty);
                        }
                    }
                    else
                    {
                        // Weekday-indexed forecast walk starting tomorrow, same math as
                        // the nightly StockoutRisk sweep (30-day horizon cap).
                        var tomorrow = Clock.Now.ToUniversalTime().Date.AddDays(1).DayOfWeek;
                        var daysUntilStockout = ForecastWalker.DaysUntilDepletion(
                            eto.NewQty, avgDailySales30, indices, tomorrow, ForecastHorizonDays);

                        if (daysUntilStockout is int depletionDay && depletionDay < rule.ThresholdValue.Value)
                        {
                            await TryCreateLogAsync(raisedTypes, rule, eto.ProductId, eto.BranchId, DecisionTypes.StockoutRisk,
                                _localizer[
                                    "DecisionReasoning:StockoutRisk",
                                    eto.NewQty,
                                    depletionDay,
                                    rule.ThresholdValue,
                                    rule.RuleName],
                                stockAtEval: eto.NewQty);
                        }
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
