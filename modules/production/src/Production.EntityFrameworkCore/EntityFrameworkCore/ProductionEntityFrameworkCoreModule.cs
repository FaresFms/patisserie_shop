using Microsoft.Extensions.DependencyInjection;
using Production.BranchRequests;
using Production.Entities;
using Production.Orders;
using Production.Plans;
using Production.Waste;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Production.EntityFrameworkCore;

[DependsOn(
    typeof(ProductionDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class ProductionEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<ProductionDbContext>(options =>
        {
            options.AddDefaultRepositories<IProductionDbContext>();

            options.AddRepository<AppProductionFormula, ProductionFormulaRepository>();
            options.AddRepository<AppBranchProductionRequest, BranchProductionRequestRepository>();
            options.AddRepository<AppProductionPlan, ProductionPlanRepository>();
            options.AddRepository<AppProductionOrder, ProductionOrderRepository>();
            options.AddRepository<AppProductionWaste, ProductionWasteRepository>();
        });
    }
}
