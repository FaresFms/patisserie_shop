using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Operations;

[DependsOn(
    typeof(OperationsApplicationContractsModule),
    typeof(AbpHttpClientModule))]
public class OperationsHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(OperationsApplicationContractsModule).Assembly,
            OperationsRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<OperationsHttpApiClientModule>();
        });

    }
}
