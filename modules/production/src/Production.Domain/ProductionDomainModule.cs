using Inventory;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Production;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(ProductionDomainSharedModule),
    typeof(InventoryDomainModule)
)]
public class ProductionDomainModule : AbpModule
{

}
