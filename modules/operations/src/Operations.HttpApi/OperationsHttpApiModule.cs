using Localization.Resources.AbpUi;
using Operations.Localization;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Operations;

[DependsOn(
    typeof(OperationsApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule))]
public class OperationsHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(OperationsHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<OperationsResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });
    }
}
