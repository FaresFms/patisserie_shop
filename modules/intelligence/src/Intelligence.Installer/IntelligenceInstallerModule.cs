using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Intelligence;

[DependsOn(
    typeof(AbpVirtualFileSystemModule)
    )]
public class IntelligenceInstallerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<IntelligenceInstallerModule>();
        });
    }
}
