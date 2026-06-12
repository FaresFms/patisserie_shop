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
        context.Services.AddTransient<TransferSuggestionScannerWorker>();
        context.Services.AddTransient<VelocityScannerWorker>();
        context.Services.AddTransient<DecisionOutcomeScannerWorker>();
        context.Services.AddTransient<ExpiryScannerWorker>();
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        await context.AddBackgroundWorkerAsync<DeadStockScannerWorker>();
        await context.AddBackgroundWorkerAsync<TransferSuggestionScannerWorker>();
        await context.AddBackgroundWorkerAsync<VelocityScannerWorker>();
        await context.AddBackgroundWorkerAsync<DecisionOutcomeScannerWorker>();
        await context.AddBackgroundWorkerAsync<ExpiryScannerWorker>();
    }
}
