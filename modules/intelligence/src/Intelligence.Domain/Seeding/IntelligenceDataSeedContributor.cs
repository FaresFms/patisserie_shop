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
        if (await _rulesRepository.AnyAsync())
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
        ), autoSave: true);
    }
}
