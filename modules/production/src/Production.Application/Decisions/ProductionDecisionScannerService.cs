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
                $"يوجد {dashboard.WaitingForIngredients} أمر طبخ ينتظر خامات. هذا يعني أن الإنتاج لا يستطيع البدء قبل تأمين المواد الناقصة.",
                "افتح شاشة الطبخ وأنشئ طلبات شراء خامات للأوامر الناقصة.");
        }

        if (dashboard.UnfulfilledDueToday > 0)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.UnfulfilledBranchRequest,
                productId,
                kitchenBranchId,
                $"يوجد {dashboard.UnfulfilledDueToday} طلب فرع مستحق اليوم ولم تُغلق كمياته بعد.",
                "راجع لوحة الصرف وحوّل الإنتاج الجاهز للفروع حسب الأولوية.");
        }

        if (dashboard.InProduction > 0)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.LateProductionRisk,
                productId,
                kitchenBranchId,
                $"يوجد {dashboard.InProduction} أمر تحت الطبخ الآن. استمرارها دون إغلاق قد يؤخر صرف طلبات الفروع.",
                "تابع أوامر الطبخ المفتوحة وسجّل الناتج المقبول والمرفوض فور انتهاء التشغيل.");
        }

        if (dashboard.RejectedToday > 0 && dashboard.YieldPercentToday < 90m)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.HighKitchenWaste,
                productId,
                kitchenBranchId,
                $"نسبة العائد اليوم {dashboard.YieldPercentToday:0.##}% فقط، مع {dashboard.RejectedToday} وحدة مرفوضة. هذا يشير إلى هدر مطبخ أعلى من المقبول.",
                "راجع سجل الهدر وحدد السبب المتكرر قبل تشغيل دفعات جديدة.");
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
                $"نسبة تلبية طلبات الفروع خلال آخر {analytics.Days} يوم هي {analytics.FulfillmentPercent:0.##}% فقط. الكمية المعتمدة {analytics.ApprovedRequestQuantity} والمنفذة {analytics.FulfilledRequestQuantity}.",
                "راجع خطة الإنتاج القادمة وزد الكميات للمنتجات ذات الطلب غير المغلق.");
        }

        if (analytics.CostVariance > 0m && analytics.CostVariancePercent > 15m)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.ProductionCostVariance,
                productId,
                kitchenBranchId,
                $"تكلفة الإنتاج الفعلية أعلى من المخططة بنسبة {analytics.CostVariancePercent:0.##}% خلال آخر {analytics.Days} يوم.",
                "راجع تكلفة الخامات والهدر والعمالة قبل اعتماد تشغيل دفعات جديدة.");
        }

        if (analytics.WastePercent > 10m && analytics.RejectedQuantity > 0)
        {
            await RaiseOnceTodayAsync(
                DecisionTypes.HighKitchenWaste,
                productId,
                kitchenBranchId,
                $"نسبة الهدر خلال آخر {analytics.Days} يوم وصلت إلى {analytics.WastePercent:0.##}% بإجمالي {analytics.RejectedQuantity} وحدة مرفوضة.",
                "افتح تحليلات الهدر وحدد السبب الأعلى ثم عدّل وصفة التشغيل أو الفحص.");
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
