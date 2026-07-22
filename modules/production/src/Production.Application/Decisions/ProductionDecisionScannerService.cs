using System;
using System.Linq;
using System.Threading.Tasks;
using Intelligence;
using Intelligence.Decisions;
using Intelligence.Entities;
using Microsoft.Extensions.Localization;
using Production.Localization;
using Production.Reports;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Production.Decisions;

public class ProductionDecisionScannerService : ITransientDependency
{
    private readonly IRepository<AppDecisionLog, Guid> _decisionLogRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IStringLocalizer<ProductionResource> _localizer;

    public ProductionDecisionScannerService(
        IRepository<AppDecisionLog, Guid> decisionLogRepository,
        IGuidGenerator guidGenerator,
        IStringLocalizer<ProductionResource> localizer)
    {
        _decisionLogRepository = decisionLogRepository;
        _guidGenerator = guidGenerator;
        _localizer = localizer;
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
                _localizer["Decision:IngredientShortage:Reason", dashboard.WaitingForIngredients],
                _localizer["Decision:IngredientShortage:Action"]);
        }

        if (dashboard.UnfulfilledDueToday > 0)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.UnfulfilledBranchRequest,
                productId,
                kitchenBranchId,
                _localizer["Decision:UnfulfilledRequest:Reason", dashboard.UnfulfilledDueToday],
                _localizer["Decision:UnfulfilledRequest:Action"]);
        }

        if (dashboard.InProduction > 0)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.LateProductionRisk,
                productId,
                kitchenBranchId,
                _localizer["Decision:LateProductionRisk:Reason", dashboard.InProduction],
                _localizer["Decision:LateProductionRisk:Action"]);
        }

        if (dashboard.RejectedToday > 0 && dashboard.YieldPercentToday < 90m)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.HighKitchenWaste,
                productId,
                kitchenBranchId,
                _localizer["Decision:HighKitchenWasteToday:Reason", dashboard.YieldPercentToday, dashboard.RejectedToday],
                _localizer["Decision:HighKitchenWasteToday:Action"]);
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
                _localizer["Decision:ProductionShortageRisk:Reason", analytics.Days, analytics.FulfillmentPercent, analytics.ApprovedRequestQuantity, analytics.FulfilledRequestQuantity],
                _localizer["Decision:ProductionShortageRisk:Action"]);
        }

        if (analytics.CostVariance > 0m && analytics.CostVariancePercent > 15m)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.ProductionCostVariance,
                productId,
                kitchenBranchId,
                _localizer["Decision:CostVariance:Reason", analytics.CostVariancePercent, analytics.Days],
                _localizer["Decision:CostVariance:Action"]);
        }

        if (analytics.WastePercent > 10m && analytics.RejectedQuantity > 0)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.HighKitchenWaste,
                productId,
                kitchenBranchId,
                _localizer["Decision:HighKitchenWastePeriod:Reason", analytics.Days, analytics.WastePercent, analytics.RejectedQuantity],
                _localizer["Decision:HighKitchenWastePeriod:Action"]);
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
