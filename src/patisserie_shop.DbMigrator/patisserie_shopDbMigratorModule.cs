using patisserie_shop.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.Modularity;

namespace patisserie_shop.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(patisserie_shopEntityFrameworkCoreModule),
    typeof(patisserie_shopApplicationContractsModule)
)]
public class patisserie_shopDbMigratorModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDataSeedOptions>(options =>
        {
            // Sales history depends on the branches/products created by
            // PatisserieDataSeedContributor — move it to the end of the
            // contributor list so it always runs last.
            options.Contributors.Remove<SalesHistorySeedContributor>();
            options.Contributors.Add<SalesHistorySeedContributor>();
        });
    }
}
