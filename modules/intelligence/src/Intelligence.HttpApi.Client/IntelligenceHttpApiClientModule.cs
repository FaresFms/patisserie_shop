using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Intelligence;

[DependsOn(
    typeof(IntelligenceApplicationContractsModule),
    typeof(AbpHttpClientModule))]
public class IntelligenceHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(IntelligenceApplicationContractsModule).Assembly,
            IntelligenceRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<IntelligenceHttpApiClientModule>();
        });

    }
}
