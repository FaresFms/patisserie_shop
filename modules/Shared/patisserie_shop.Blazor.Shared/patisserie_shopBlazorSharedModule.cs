using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Volo.Abp.AspNetCore.Components.Server.LeptonXLiteTheme;
using Volo.Abp.Modularity;

namespace patisserie_shop.Blazor.Shared;

[DependsOn(
    typeof(AbpAspNetCoreComponentsServerLeptonXLiteThemeModule)
)]
public class patisserie_shopBlazorSharedModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMudServices();
    }
}
