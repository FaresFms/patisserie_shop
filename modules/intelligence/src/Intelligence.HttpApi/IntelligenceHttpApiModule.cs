using Localization.Resources.AbpUi;
using Intelligence.Localization;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Intelligence;

[DependsOn(
    typeof(IntelligenceApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule))]
public class IntelligenceHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(IntelligenceHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<IntelligenceResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });
    }
}
