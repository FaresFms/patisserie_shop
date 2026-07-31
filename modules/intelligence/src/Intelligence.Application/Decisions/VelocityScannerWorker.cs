using System.Threading.Tasks;
using Intelligence.Settings;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace Intelligence.Decisions;

/// <summary>
/// Periodic worker that drives <see cref="VelocityScannerService"/>. As with the other
/// scanner workers, the logic lives in the service; the worker owns only the schedule
/// and the unit-of-work boundary. One run = velocity computation, then the StockoutRisk
/// sweep (sequential, same unit of work).
///
/// Interval comes from the ABP setting
/// <see cref="IntelligenceSettings.VelocityScanIntervalMinutes"/>.
/// </summary>
public class VelocityScannerWorker : SettingBasedScannerWorkerBase
{
    public VelocityScannerWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IntelligenceBackgroundJobScheduleNotifier scheduleNotifier)
        : base(
            timer,
            serviceScopeFactory,
            scheduleNotifier,
            IntelligenceSettings.VelocityScanIntervalMinutes,
            IntelligenceSettings.VelocityDefaultIntervalMinutes)
    {
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var sp = workerContext.ServiceProvider;
        var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
        var scanner = sp.GetRequiredService<VelocityScannerService>();

        using var uow = uowManager.Begin(requiresNew: true);
        await scanner.ScanAsync();
        await uow.CompleteAsync();
    }
}
