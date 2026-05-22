using Microsoft.Extensions.DependencyInjection;
using Inventory;
using Volo.Abp.Identity;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.Application;
using Volo.Abp.FluentValidation;

namespace Operations;

[DependsOn(
    typeof(OperationsDomainModule),
    typeof(OperationsApplicationContractsModule),
    typeof(InventoryDomainModule),
    typeof(AbpIdentityDomainModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule),
    typeof(AbpFluentValidationModule)
    )]
public class OperationsApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<OperationsApplicationModule>();
    }
}
