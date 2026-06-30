using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Production;

[DependsOn(
    typeof(AbpVirtualFileSystemModule)
    )]
public class ProductionInstallerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<ProductionInstallerModule>();
        });
    }
}
