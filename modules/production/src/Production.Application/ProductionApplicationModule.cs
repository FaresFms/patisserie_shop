using Inventory;
using Intelligence;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;

namespace Production;

[DependsOn(
    typeof(ProductionDomainModule),
    typeof(ProductionApplicationContractsModule),
    typeof(InventoryApplicationContractsModule),
    typeof(IntelligenceDomainModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule),
    typeof(AbpBackgroundWorkersModule)
    )]
public class ProductionApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<ProductionApplicationModule>();
    }
}
