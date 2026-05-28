using Inventory;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.Application;

namespace Intelligence;

[DependsOn(
    typeof(IntelligenceDomainModule),
    typeof(IntelligenceApplicationContractsModule),
    typeof(InventoryApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule)
    )]
public class IntelligenceApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<IntelligenceApplicationModule>();
    }
}
