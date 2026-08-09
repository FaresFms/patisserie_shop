using patisserie_shop.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        var configuration = context.Services.GetConfiguration();
        var seedDemoData = configuration.GetValue<bool>("DataSeeding:SeedDemoData");

        Configure<AbpDataSeedOptions>(options =>
        {
            // Keep the presentation pipeline deterministic on a completely empty database:
            // ABP creates its built-in admin first, then we create accounts, master data,
            // production workflows, and finally the historical sales ledger.
            options.Contributors.Remove<IdentityDataSeedContributor>();
            options.Contributors.Remove<PatisserieDataSeedContributor>();
            options.Contributors.Remove<ProductionDemoSeedContributor>();
            options.Contributors.Remove<SalesHistorySeedContributor>();
            options.Contributors.Remove<GraduationOperationsSeedContributor>();
            options.Contributors.Add<IdentityDataSeedContributor>();
            options.Contributors.Add<PatisserieDataSeedContributor>();
            if (seedDemoData)
            {
                options.Contributors.Add<ProductionDemoSeedContributor>();
                options.Contributors.Add<SalesHistorySeedContributor>();
                options.Contributors.Add<GraduationOperationsSeedContributor>();
            }
        });
    }
}
