using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.Application;
using Volo.Abp.FluentValidation;

namespace Inventory;

[DependsOn(
    typeof(InventoryDomainModule),
    typeof(InventoryApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule),
    typeof(AbpFluentValidationModule)
    )]
public class InventoryApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<InventoryApplicationModule>();
    }
}
