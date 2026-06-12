using Microsoft.Extensions.DependencyInjection;
using Operations.Entities;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Operations.EntityFrameworkCore;

[DependsOn(
    typeof(OperationsDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class OperationsEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<OperationsDbContext>(options =>
        {
            options.AddDefaultRepositories<IOperationsDbContext>();
            options.AddRepository<AppSale, SaleRepository>();
            options.AddRepository<AppStockTransfer, StockTransferRepository>();
        });
    }
}
