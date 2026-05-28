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
