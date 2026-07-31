using System.Threading.Tasks;
using Intelligence.Settings;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace Intelligence.Decisions;

/// <summary>
/// Periodic worker that drives <see cref="ExpiryScannerService"/>. The scanning logic
/// lives in the service (testable, mirrors how the other scanner/worker pairs split);
/// the worker only owns the schedule and the unit-of-work boundary.
///
/// Interval comes from the ABP setting
/// <see cref="IntelligenceSettings.ExpiryScanIntervalMinutes"/>.
/// </summary>
public class ExpiryScannerWorker : SettingBasedScannerWorkerBase
{
    public ExpiryScannerWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IntelligenceBackgroundJobScheduleNotifier scheduleNotifier)
        : base(
            timer,
            serviceScopeFactory,
            scheduleNotifier,
            IntelligenceSettings.ExpiryScanIntervalMinutes,
            IntelligenceSettings.ExpiryDefaultIntervalMinutes)
    {
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var sp = workerContext.ServiceProvider;
        var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
        var scanner = sp.GetRequiredService<ExpiryScannerService>();

        using var uow = uowManager.Begin(requiresNew: true);
        await scanner.ScanAsync();
        await uow.CompleteAsync();
    }
}
