using System.Threading.Tasks;
using Intelligence.Settings;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace Intelligence.Decisions;

/// <summary>
/// Periodic worker that drives <see cref="DeadStockScannerService"/>. The scanning logic
/// lives in the service (testable, mirrors how StockChangedEventHandler delegates to
/// DecisionMakerService); the worker only owns the schedule and the unit-of-work boundary.
///
/// Interval comes from the ABP setting
/// <see cref="IntelligenceSettings.DeadStockScanIntervalMinutes"/>.
/// </summary>
public class DeadStockScannerWorker : SettingBasedScannerWorkerBase
{
    public DeadStockScannerWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IntelligenceBackgroundJobScheduleNotifier scheduleNotifier)
        : base(
            timer,
            serviceScopeFactory,
            scheduleNotifier,
            IntelligenceSettings.DeadStockScanIntervalMinutes,
            IntelligenceSettings.DeadStockDefaultIntervalMinutes)
    {
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var sp = workerContext.ServiceProvider;
        var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
        var scanner = sp.GetRequiredService<DeadStockScannerService>();

        using var uow = uowManager.Begin(requiresNew: true);
        await scanner.ScanAsync();
        await uow.CompleteAsync();
    }
}
