using Intelligence.Decisions;
using Intelligence.Entities;
using Intelligence.Rules;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace Intelligence;

[Mapper]
public partial class InventoryRuleToDtoMapper : MapperBase<AppInventoryRule, InventoryRuleDto>
{
    // ProductName / BranchName are joined data resolved manually by the app service.
    [MapperIgnoreTarget(nameof(InventoryRuleDto.ProductName))]
    [MapperIgnoreTarget(nameof(InventoryRuleDto.BranchName))]
    public override partial InventoryRuleDto Map(AppInventoryRule source);

    [MapperIgnoreTarget(nameof(InventoryRuleDto.ProductName))]
    [MapperIgnoreTarget(nameof(InventoryRuleDto.BranchName))]
    public override partial void Map(AppInventoryRule source, InventoryRuleDto destination);
}

[Mapper]
public partial class DecisionLogToDtoMapper : MapperBase<AppDecisionLog, DecisionLogDto>
{
    // Joined/lookup data resolved manually by the app service.
    [MapperIgnoreTarget(nameof(DecisionLogDto.RuleName))]
    [MapperIgnoreTarget(nameof(DecisionLogDto.ProductName))]
    [MapperIgnoreTarget(nameof(DecisionLogDto.BranchName))]
    [MapperIgnoreTarget(nameof(DecisionLogDto.SourceBranchName))]
    [MapperIgnoreTarget(nameof(DecisionLogDto.TargetBranchName))]
    [MapperIgnoreTarget(nameof(DecisionLogDto.AcknowledgedByUserName))]
    public override partial DecisionLogDto Map(AppDecisionLog source);

    [MapperIgnoreTarget(nameof(DecisionLogDto.RuleName))]
    [MapperIgnoreTarget(nameof(DecisionLogDto.ProductName))]
    [MapperIgnoreTarget(nameof(DecisionLogDto.BranchName))]
    [MapperIgnoreTarget(nameof(DecisionLogDto.SourceBranchName))]
    [MapperIgnoreTarget(nameof(DecisionLogDto.TargetBranchName))]
    [MapperIgnoreTarget(nameof(DecisionLogDto.AcknowledgedByUserName))]
    public override partial void Map(AppDecisionLog source, DecisionLogDto destination);
}
