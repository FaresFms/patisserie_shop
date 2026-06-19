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
/// Seeds a realistic starting set of AppInventoryRules so the decision engine
/// has something to evaluate from day one. Skips entirely if any rule already
/// exists — re-runs of the DbMigrator do not duplicate or overwrite seeded
/// rules (or any rules edited by an admin).
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
        if (await _rulesRepository.FindAsync(IntelligenceConstants.CashierReportRuleId) == null)
        {
            await _rulesRepository.InsertAsync(new AppInventoryRule(
                id: IntelligenceConstants.CashierReportRuleId,
                ruleName: "Cashier Stock Report",
                ruleType: InventoryRuleTypes.LowStock,
                productId: null,
                branchId: null,
                thresholdValue: 0,
                thresholdDays: null,
                suggestedAction: "Restock",
                priority: 0,
                isActive: false
            ), autoSave: true);
        }

        if (await _rulesRepository.AnyAsync(r => r.Id != IntelligenceConstants.CashierReportRuleId))
        {
            return;
        }

        // Rule 1 — global LowStock at 5 units. Priority 0 (default).
        await _rulesRepository.InsertAsync(new AppInventoryRule(
            id: _guidGenerator.Create(),
            ruleName: "Global Low Stock Alert",
            ruleType: InventoryRuleTypes.LowStock,
            productId: null,
            branchId: null,
            thresholdValue: 5,
            thresholdDays: null,
            suggestedAction: "Reorder from default supplier or request transfer from warehouse",
            priority: 0,
            isActive: true
        ), autoSave: false);

        // Rule 2 — global ExcessStock at 100 units.
        await _rulesRepository.InsertAsync(new AppInventoryRule(
            id: _guidGenerator.Create(),
            ruleName: "Global Excess Stock Alert",
            ruleType: InventoryRuleTypes.ExcessStock,
            productId: null,
            branchId: null,
            thresholdValue: 100,
            thresholdDays: null,
            suggestedAction: "Consider transferring excess stock to another branch",
            priority: 0,
            isActive: true
        ), autoSave: false);

        // Rule 3 — global DeadStock at 30 days without sale.
        await _rulesRepository.InsertAsync(new AppInventoryRule(
            id: _guidGenerator.Create(),
            ruleName: "Global Dead Stock (30 days)",
            ruleType: InventoryRuleTypes.DeadStock,
            productId: null,
            branchId: null,
            thresholdValue: null,
            thresholdDays: 30,
            suggestedAction: "Apply 20% discount or redistribute to active branch",
            priority: 0,
            isActive: true
        ), autoSave: false);

        // Rule 4 — high-priority "Critical Low Stock" at 2 units, Priority 10.
        // Wins over Rule 1 when both match, so any product that drops to ≤2
        // gets the URGENT messaging instead of the generic Low Stock note.
        await _rulesRepository.InsertAsync(new AppInventoryRule(
            id: _guidGenerator.Create(),
            ruleName: "Critical Low Stock Alert",
            ruleType: InventoryRuleTypes.LowStock,
            productId: null,
            branchId: null,
            thresholdValue: 2,
            thresholdDays: null,
            suggestedAction: "URGENT: Stock critically low — reorder immediately",
            priority: 10,
            isActive: true
        ), autoSave: false);

        // Rule 5 — global TransferSuggestion at 10 units, Priority 5.
        // Any branch below 10 units of a product is a candidate to receive
        // a transfer from a branch that has clear excess.
        await _rulesRepository.InsertAsync(new AppInventoryRule(
            id: _guidGenerator.Create(),
            ruleName: "Global Transfer Suggestion",
            ruleType: InventoryRuleTypes.TransferSuggestion,
            productId: null,
            branchId: null,
            thresholdValue: 10,
            thresholdDays: null,
            suggestedAction: "Transfer stock from overstocked branch",
            priority: 5,
            isActive: true
        ), autoSave: true);
    }
}
