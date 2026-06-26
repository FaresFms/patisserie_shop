using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Production;

[DependsOn(
    typeof(ProductionApplicationContractsModule),
    typeof(AbpHttpClientModule))]
public class ProductionHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(ProductionApplicationContractsModule).Assembly,
            ProductionRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<ProductionHttpApiClientModule>();
        });

    }
}
