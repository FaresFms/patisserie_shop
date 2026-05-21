using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Operations;

[DependsOn(
    typeof(AbpVirtualFileSystemModule)
    )]
public class OperationsInstallerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<OperationsInstallerModule>();
        });
    }
}
