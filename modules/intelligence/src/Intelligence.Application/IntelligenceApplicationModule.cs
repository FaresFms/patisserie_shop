using System.Threading.Tasks;
using Intelligence.Decisions;
using Inventory;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;

namespace Intelligence;

[DependsOn(
    typeof(IntelligenceDomainModule),
    typeof(IntelligenceApplicationContractsModule),
    typeof(InventoryApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule),
    typeof(AbpBackgroundWorkersModule)
    )]
public class IntelligenceApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<IntelligenceApplicationModule>();
        context.Services.AddTransient<DeadStockScannerWorker>();
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        await context.AddBackgroundWorkerAsync<DeadStockScannerWorker>();
    }
}
