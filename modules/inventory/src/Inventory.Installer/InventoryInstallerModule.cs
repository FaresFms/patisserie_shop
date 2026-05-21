using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Inventory;

[DependsOn(
    typeof(AbpVirtualFileSystemModule)
    )]
public class InventoryInstallerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<InventoryInstallerModule>();
        });
    }
}
