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
            // Production demo data depends on the branches/products/users created
            // by the earlier contributors. Sales history still runs last.
            options.Contributors.Remove<ProductionDemoSeedContributor>();
            options.Contributors.Remove<SalesHistorySeedContributor>();
            options.Contributors.Add<ProductionDemoSeedContributor>();
            options.Contributors.Add<SalesHistorySeedContributor>();
        });
    }
}
