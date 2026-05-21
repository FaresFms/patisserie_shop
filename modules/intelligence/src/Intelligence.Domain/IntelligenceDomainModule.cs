using Inventory;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Intelligence;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(IntelligenceDomainSharedModule),
    typeof(InventoryDomainSharedModule)
)]
public class IntelligenceDomainModule : AbpModule
{

}
