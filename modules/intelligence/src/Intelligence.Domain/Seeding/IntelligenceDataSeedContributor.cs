using System;
using System.Threading.Tasks;
using Intelligence.Entities;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace Intelligence.Seeding;

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

        await _rulesRepository.InsertAsync(new AppInventoryRule(
            id: _guidGenerator.Create(),
            ruleName: "Global Low Stock Alert",
            ruleType: "LowStock",
            productId: null,
            branchId: null,
            thresholdValue: 5,
            thresholdDays: null,
            suggestedAction: "Reorder stock from default supplier",
            priority: 0,
            isActive: true
        ), autoSave: false);

        await _rulesRepository.InsertAsync(new AppInventoryRule(
            id: _guidGenerator.Create(),
            ruleName: "Global Excess Stock Alert",
            ruleType: "ExcessStock",
            productId: null,
            branchId: null,
            thresholdValue: 100,
            thresholdDays: null,
            suggestedAction: "Consider transferring excess to another branch",
            priority: 0,
            isActive: true
        ), autoSave: true);
    }
}
