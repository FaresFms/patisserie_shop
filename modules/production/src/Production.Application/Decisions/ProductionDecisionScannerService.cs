using System;
using System.Linq;
using System.Threading.Tasks;
using Intelligence;
using Intelligence.Decisions;
using Intelligence.Entities;
using Production.Reports;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Production.Decisions;

public class ProductionDecisionScannerService : ITransientDependency
{
    private readonly IRepository<AppDecisionLog, Guid> _decisionLogRepository;
    private readonly IGuidGenerator _guidGenerator;

    public ProductionDecisionScannerService(
        IRepository<AppDecisionLog, Guid> decisionLogRepository,
        IGuidGenerator guidGenerator)
    {
        _decisionLogRepository = decisionLogRepository;
        _guidGenerator = guidGenerator;
    }

    public async Task ScanDashboardAsync(ProductionDashboardReadModel dashboard, Guid? kitchenBranchId)
    {
        var productId = dashboard.ProductFocus.FirstOrDefault()?.ProductId ?? Guid.Empty;

        if (dashboard.WaitingForIngredients > 0)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.IngredientShortage,
                productId,
                kitchenBranchId,
                $"{dashboard.WaitingForIngredients} cook order(s) are waiting for ingredients — production cannot start until the missing materials are secured.",
                "Open the Cook screen and create ingredient purchase orders for the blocked orders.");
        }

        if (dashboard.UnfulfilledDueToday > 0)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.UnfulfilledBranchRequest,
                productId,
                kitchenBranchId,
                $"{dashboard.UnfulfilledDueToday} branch request(s) are due today and their quantities are not fully dispatched yet.",
                "Review the Dispatch board and send the finished goods to branches by priority.");
        }

        if (dashboard.InProduction > 0)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.LateProductionRisk,
                productId,
                kitchenBranchId,
                $"{dashboard.InProduction} order(s) are cooking right now. Leaving them open too long can delay dispatching branch requests.",
                "Follow up the open cook orders and record accepted/rejected output as soon as each batch finishes.");
        }

        if (dashboard.RejectedToday > 0 && dashboard.YieldPercentToday < 90m)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.HighKitchenWaste,
                productId,
                kitchenBranchId,
                $"Today's yield is only {dashboard.YieldPercentToday:0.##}%, with {dashboard.RejectedToday} rejected unit(s) — kitchen waste is higher than acceptable.",
                "Review the waste log and find the recurring cause before starting new batches.");
        }
    }

    public async Task ScanAnalyticsAsync(ProductionAnalyticsReadModel analytics, Guid? kitchenBranchId)
    {
        var productId = analytics.ProductPerformance.FirstOrDefault()?.ProductId ?? Guid.Empty;

        if (analytics.ApprovedRequestQuantity > analytics.FulfilledRequestQuantity
            && analytics.FulfillmentPercent < 95m)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.ProductionShortageRisk,
                productId,
                kitchenBranchId,
                $"Branch-request fulfillment over the last {analytics.Days} day(s) is only {analytics.FulfillmentPercent:0.##}%. Approved quantity {analytics.ApprovedRequestQuantity}, fulfilled {analytics.FulfilledRequestQuantity}.",
                "Review the next production plan and increase quantities for products with unmet demand.");
        }

        if (analytics.CostVariance > 0m && analytics.CostVariancePercent > 15m)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.ProductionCostVariance,
                productId,
                kitchenBranchId,
                $"Actual production cost is {analytics.CostVariancePercent:0.##}% above plan over the last {analytics.Days} day(s).",
                "Review ingredient, waste and labour costs before approving new batches.");
        }

        if (analytics.WastePercent > 10m && analytics.RejectedQuantity > 0)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.HighKitchenWaste,
                productId,
                kitchenBranchId,
                $"Waste over the last {analytics.Days} day(s) reached {analytics.WastePercent:0.##}%, with {analytics.RejectedQuantity} rejected unit(s) in total.",
                "Open waste analytics, find the top cause, then adjust the recipe or the quality check.");
        }
    }

    private async Task RaiseOnceTodayAsync(
        string decisionType,
        Guid productId,
        Guid? branchId,
        string reasoning,
        string suggestedAction)
    {
        var today = DateTime.UtcNow.Date;
        var ruleId = IntelligenceConstants.ProductionOperationsRuleId;

        var exists = branchId.HasValue
            ? await _decisionLogRepository.AnyAsync(l =>
                l.RuleId == ruleId
                && l.DecisionType == decisionType
                && l.BranchId == branchId.Value
                && l.CreationTime >= today)
            : await _decisionLogRepository.AnyAsync(l =>
                l.RuleId == ruleId
                && l.DecisionType == decisionType
                && l.BranchId == null
                && l.CreationTime >= today);

        if (exists)
        {
            return;
        }

        var log = new AppDecisionLog(
            id: _guidGenerator.Create(),
            ruleId: ruleId,
            productId: productId,
            branchId: branchId,
            decisionType: decisionType,
            reasoning: reasoning,
            suggestedAction: suggestedAction);

        await _decisionLogRepository.InsertAsync(log, autoSave: false);
    }
}
