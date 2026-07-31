using System;
using System.Threading.Tasks;
using Intelligence.Entities;
using Intelligence.Rules;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace Intelligence.Seeding;

/// <summary>
/// Seeds the fixed operational sentinel rules plus a realistic starting rule bank.
/// Re-runs upgrade only exact, untouched legacy demo wording; admin-edited rules are
/// preserved, and the starting bank is created only when no non-sentinel rules exist.
/// </summary>
public class IntelligenceDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<AppInventoryRule, Guid> _rulesRepository;
    private readonly IGuidGenerator _guidGenerator;

    public IntelligenceDataSeedContributor(
        IRepository<AppInventoryRule, Guid> rulesRepository,
        IGuidGenerator guidGenerator)
    {
        _rulesRepository = rulesRepository;
        _guidGenerator = guidGenerator;
    }

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        // Sentinel rule for manual cashier stock reports. Seeded with a FIXED id and
        // independently of the rule-bank guard below, so it lands even on databases that
        // already have rules. INACTIVE on purpose — the engine never evaluates it; it only
        // satisfies AppDecisionLog's non-null RuleId for manual reports (DecisionType
        // StockReport) and gives the decision log a readable rule name. Idempotent: keyed
        // on the fixed id. RuleType LowStock + a dummy threshold keeps the entity invariant
        // happy (ThresholdValue is required for that type); IsActive=false guarantees no
        // real evaluation regardless of the type chosen.
        await EnsureSentinelRuleAsync(
            IntelligenceConstants.CashierReportRuleId,
            legacyRuleName: "Cashier stock report",
            legacySuggestedAction: "Restock this product",
            legacyArabicRuleName: "تقرير مخزون الكاشير",
            legacyArabicSuggestedAction: "إعادة التخزين",
            arabicRuleName: "بلاغ الكاشير عن المخزون",
            arabicSuggestedAction: "زوّد الفرع من هالصنف بأقرب وقت.");

        await EnsureSentinelRuleAsync(
            IntelligenceConstants.CashierVarianceRuleId,
            legacyRuleName: "Cashier shift variance alert",
            legacySuggestedAction: "Review cashier shift reconciliation",
            legacyArabicRuleName: "تنبيه فرق وردية الكاشير",
            legacyArabicSuggestedAction: "مراجعة تسوية وردية الكاشير",
            arabicRuleName: "فرق صندوق وردية الكاشير",
            arabicSuggestedAction: "راجع فرق الصندوق وتأكد من تسوية الوردية.");

        await EnsureSentinelRuleAsync(
            IntelligenceConstants.ProductionOperationsRuleId,
            legacyRuleName: "Production operations alerts",
            legacySuggestedAction: "Review the production dashboard and take the suggested step",
            legacyArabicRuleName: "تنبيهات الإنتاج التشغيلية",
            legacyArabicSuggestedAction: "راجع لوحة الإنتاج واتخذ الإجراء المناسب",
            arabicRuleName: "تنبيهات شغل الإنتاج",
            arabicSuggestedAction: "راجع لوحة الإنتاج ونفّذ الخطوة المقترحة.");

        await UpgradeLegacyStarterRulesAsync();

        if (await _rulesRepository.AnyAsync(r => r.Id != IntelligenceConstants.CashierReportRuleId
                                             && r.Id != IntelligenceConstants.CashierVarianceRuleId
                                             && r.Id != IntelligenceConstants.ProductionOperationsRuleId))
        {
            return;
        }

        // Rule 1 — global LowStock at 5 units. Priority 0 (default).
        await _rulesRepository.InsertAsync(new AppInventoryRule(
            id: _guidGenerator.Create(),
            ruleName: "تنبيه نقص المخزون بكل الفروع",
            ruleType: InventoryRuleTypes.LowStock,
            productId: null,
            branchId: null,
            thresholdValue: 5,
            thresholdDays: null,
            suggestedAction: "اطلب كمية جديدة من المورّد، أو انقل مخزون من فرع عنده كمية زيادة.",
            priority: 0,
            isActive: true
        ), autoSave: false);

        // Rule 2 — global ExcessStock at 100 units.
        await _rulesRepository.InsertAsync(new AppInventoryRule(
            id: _guidGenerator.Create(),
            ruleName: "تنبيه زيادة المخزون بكل الفروع",
            ruleType: InventoryRuleTypes.ExcessStock,
            productId: null,
            branchId: null,
            thresholdValue: 100,
            thresholdDays: null,
            suggestedAction: "انقل الكمية الزايدة لفرع بحاجة إلها.",
            priority: 0,
            isActive: true
        ), autoSave: false);

        // Rule 3 — global DeadStock at 30 days without sale.
        await _rulesRepository.InsertAsync(new AppInventoryRule(
            id: _guidGenerator.Create(),
            ruleName: "مخزون ما تحرّك من 30 يوم",
            ruleType: InventoryRuleTypes.DeadStock,
            productId: null,
            branchId: null,
            thresholdValue: null,
            thresholdDays: 30,
            suggestedAction: "اعمل خصم 20٪ أو انقل الصنف لفرع مبيعاته أحسن.",
            priority: 0,
            isActive: true
        ), autoSave: false);

        // Rule 4 — high-priority "Critical Low Stock" at 2 units, Priority 10.
        // Wins over Rule 1 when both match, so any product that drops to ≤2
        // gets the URGENT messaging instead of the generic Low Stock note.
        await _rulesRepository.InsertAsync(new AppInventoryRule(
            id: _guidGenerator.Create(),
            ruleName: "تنبيه نقص مخزون خطير",
            ruleType: InventoryRuleTypes.LowStock,
            productId: null,
            branchId: null,
            thresholdValue: 2,
            thresholdDays: null,
            suggestedAction: "الكمية صارت قليلة جدًا. اطلب أو انقل مخزون فورًا.",
            priority: 10,
            isActive: true
        ), autoSave: false);

        // Rule 5 — global TransferSuggestion at 10 units, Priority 5.
        // Any branch below 10 units of a product is a candidate to receive
        // a transfer from a branch that has clear excess.
        await _rulesRepository.InsertAsync(new AppInventoryRule(
            id: _guidGenerator.Create(),
            ruleName: "اقتراح نقل مخزون بين الفروع",
            ruleType: InventoryRuleTypes.TransferSuggestion,
            productId: null,
            branchId: null,
            thresholdValue: 10,
            thresholdDays: null,
            suggestedAction: "انقل كمية من فرع عنده زيادة للفرع اللي بحاجة.",
            priority: 5,
            isActive: true
        ), autoSave: true);
    }

    private async Task EnsureSentinelRuleAsync(
        Guid id,
        string legacyRuleName,
        string legacySuggestedAction,
        string legacyArabicRuleName,
        string legacyArabicSuggestedAction,
        string arabicRuleName,
        string arabicSuggestedAction)
    {
        var rule = await _rulesRepository.FindAsync(id);
        if (rule == null)
        {
            await _rulesRepository.InsertAsync(new AppInventoryRule(
                id: id,
                ruleName: arabicRuleName,
                ruleType: InventoryRuleTypes.LowStock,
                productId: null,
                branchId: null,
                thresholdValue: 0,
                thresholdDays: null,
                suggestedAction: arabicSuggestedAction,
                priority: 0,
                isActive: false
            ), autoSave: true);
            return;
        }

        if (rule.CreatorId != null
            || rule.LastModificationTime != null
            || rule.LastModifierId != null
            || rule.RuleType != InventoryRuleTypes.LowStock
            || rule.ProductId != null
            || rule.BranchId != null
            || rule.ThresholdValue != 0
            || rule.ThresholdDays != null
            || rule.Priority != 0
            || rule.IsActive
            || rule.ActionMode != RuleActionModes.SuggestOnly)
        {
            return;
        }

        var changed = false;
        if (rule.RuleName == legacyRuleName || rule.RuleName == legacyArabicRuleName)
        {
            rule.SetRuleName(arabicRuleName);
            changed = true;
        }

        if (rule.SuggestedAction == legacySuggestedAction
            || rule.SuggestedAction == legacyArabicSuggestedAction)
        {
            rule.SetSuggestedAction(arabicSuggestedAction);
            changed = true;
        }

        if (changed)
        {
            await _rulesRepository.UpdateAsync(rule, autoSave: true);
        }
    }

    /// <summary>
    /// Upgrades only the exact English text shipped by the old demo seeder. Structural
    /// checks keep similarly named user rules out of scope, and edited fields are left as-is.
    /// </summary>
    private async Task UpgradeLegacyStarterRulesAsync()
    {
        var translations = new[]
        {
            new LegacyRuleTranslation(
                InventoryRuleTypes.LowStock, 5, null, 0,
                "Global low stock alert",
                "Reorder from the default supplier or request a transfer from another branch",
                "تنبيه نقص المخزون بكل الفروع",
                "اطلب كمية جديدة من المورّد، أو انقل مخزون من فرع عنده كمية زيادة."),
            new LegacyRuleTranslation(
                InventoryRuleTypes.ExcessStock, 100, null, 0,
                "Global excess stock alert",
                "Consider transferring the surplus to another branch",
                "تنبيه زيادة المخزون بكل الفروع",
                "انقل الكمية الزايدة لفرع بحاجة إلها."),
            new LegacyRuleTranslation(
                InventoryRuleTypes.DeadStock, null, 30, 0,
                "Global dead stock (30 days)",
                "Apply a 20% discount or redistribute to a busier branch",
                "مخزون ما تحرّك من 30 يوم",
                "اعمل خصم 20٪ أو انقل الصنف لفرع مبيعاته أحسن."),
            new LegacyRuleTranslation(
                InventoryRuleTypes.LowStock, 2, null, 10,
                "Critical low stock alert",
                "URGENT: stock is critically low — reorder immediately",
                "تنبيه نقص مخزون خطير",
                "الكمية صارت قليلة جدًا. اطلب أو انقل مخزون فورًا."),
            new LegacyRuleTranslation(
                InventoryRuleTypes.TransferSuggestion, 10, null, 5,
                "Global transfer suggestion",
                "Transfer stock from a branch that has a surplus",
                "اقتراح نقل مخزون بين الفروع",
                "انقل كمية من فرع عنده زيادة للفرع اللي بحاجة."),
            new LegacyRuleTranslation(
                InventoryRuleTypes.LowStock, 5, null, 0,
                "تنبيه انخفاض المخزون العام",
                "أعِد الطلب من المورّد الافتراضي أو اطلب تحويلاً من المستودع",
                "تنبيه نقص المخزون بكل الفروع",
                "اطلب كمية جديدة من المورّد، أو انقل مخزون من فرع عنده كمية زيادة."),
            new LegacyRuleTranslation(
                InventoryRuleTypes.ExcessStock, 100, null, 0,
                "تنبيه فائض المخزون العام",
                "فكّر في تحويل المخزون الفائض إلى فرع آخر",
                "تنبيه زيادة المخزون بكل الفروع",
                "انقل الكمية الزايدة لفرع بحاجة إلها."),
            new LegacyRuleTranslation(
                InventoryRuleTypes.DeadStock, null, 30, 0,
                "المخزون الراكد العام (30 يومًا)",
                "طبّق خصم 20% أو أعِد التوزيع إلى فرع نشط",
                "مخزون ما تحرّك من 30 يوم",
                "اعمل خصم 20٪ أو انقل الصنف لفرع مبيعاته أحسن."),
            new LegacyRuleTranslation(
                InventoryRuleTypes.LowStock, 2, null, 10,
                "تنبيه انخفاض حرج للمخزون",
                "عاجل: المخزون منخفض بشكل حرج — أعِد الطلب فوراً",
                "تنبيه نقص مخزون خطير",
                "الكمية صارت قليلة جدًا. اطلب أو انقل مخزون فورًا."),
            new LegacyRuleTranslation(
                InventoryRuleTypes.TransferSuggestion, 10, null, 5,
                "اقتراح تحويل عام",
                "حوّل المخزون من فرع لديه فائض",
                "اقتراح نقل مخزون بين الفروع",
                "انقل كمية من فرع عنده زيادة للفرع اللي بحاجة.")
        };

        var rules = await _rulesRepository.GetListAsync(r =>
            r.Id != IntelligenceConstants.CashierReportRuleId
            && r.Id != IntelligenceConstants.CashierVarianceRuleId
            && r.Id != IntelligenceConstants.ProductionOperationsRuleId
            && r.ProductId == null
            && r.BranchId == null);

        foreach (var rule in rules)
        {
            foreach (var translation in translations)
            {
                if (!translation.Matches(rule))
                {
                    continue;
                }

                var changed = false;
                if (rule.RuleName == translation.LegacyRuleName)
                {
                    rule.SetRuleName(translation.ArabicRuleName);
                    changed = true;
                }

                if (rule.SuggestedAction == translation.LegacySuggestedAction)
                {
                    rule.SetSuggestedAction(translation.ArabicSuggestedAction);
                    changed = true;
                }

                if (changed)
                {
                    await _rulesRepository.UpdateAsync(rule, autoSave: true);
                }

                break;
            }
        }
    }

    private sealed record LegacyRuleTranslation(
        string RuleType,
        int? ThresholdValue,
        int? ThresholdDays,
        int Priority,
        string LegacyRuleName,
        string LegacySuggestedAction,
        string ArabicRuleName,
        string ArabicSuggestedAction)
    {
        public bool Matches(AppInventoryRule rule)
            => rule.RuleType == RuleType
               && rule.ThresholdValue == ThresholdValue
               && rule.ThresholdDays == ThresholdDays
               && rule.Priority == Priority
               && rule.IsActive
               && rule.ActionMode == RuleActionModes.SuggestOnly
               && rule.CreatorId == null
               && rule.LastModificationTime == null
               && rule.LastModifierId == null
               && (rule.RuleName == LegacyRuleName || rule.RuleName == ArabicRuleName)
               && (rule.SuggestedAction == LegacySuggestedAction
                   || rule.SuggestedAction == ArabicSuggestedAction);
    }
}
