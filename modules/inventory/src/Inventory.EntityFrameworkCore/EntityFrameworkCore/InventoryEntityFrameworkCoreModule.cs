using Inventory.Entities;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Inventory.EntityFrameworkCore;

[DependsOn(
    typeof(InventoryDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class InventoryEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<InventoryDbContext>(options =>
        {
            options.AddDefaultRepositories<IInventoryDbContext>();
            options.AddRepository<AppBranchInventory, BranchInventoryRepository>();
            options.AddRepository<AppBranch, BranchRepository>();
            options.AddRepository<AppCategory, CategoryRepository>();
            options.AddRepository<AppStockMovement, StockMovementRepository>();
            options.AddRepository<AppProduct, ProductRepository>();
            options.AddRepository<AppSupplier, SupplierRepository>();
            options.AddRepository<AppStockBatch, StockBatchRepository>();
            options.AddRepository<AppStocktakeSession, StocktakeSessionRepository>();
        });
    }
}
