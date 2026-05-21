using patisserie_shop.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace patisserie_shop.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(patisserie_shopEntityFrameworkCoreModule),
    typeof(patisserie_shopApplicationContractsModule)
)]
public class patisserie_shopDbMigratorModule : AbpModule
{
}
