using System.Threading.Tasks;
using Intelligence.Settings;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace Intelligence.Decisions;

/// <summary>
/// Periodic worker that drives <see cref="DecisionOutcomeScannerService"/>. As with the
/// other scanner workers, the logic lives in the service; the worker owns only the
/// schedule and the unit-of-work boundary.
///
/// Interval comes from the ABP setting
/// <see cref="IntelligenceSettings.DecisionOutcomeScanIntervalMinutes"/>.
/// </summary>
public class DecisionOutcomeScannerWorker : SettingBasedScannerWorkerBase
{
    public DecisionOutcomeScannerWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IntelligenceBackgroundJobScheduleNotifier scheduleNotifier)
        : base(
            timer,
            serviceScopeFactory,
            scheduleNotifier,
            IntelligenceSettings.DecisionOutcomeScanIntervalMinutes,
            IntelligenceSettings.DecisionOutcomeDefaultIntervalMinutes)
    {
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var sp = workerContext.ServiceProvider;
        var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
        var scanner = sp.GetRequiredService<DecisionOutcomeScannerService>();

        using var uow = uowManager.Begin(requiresNew: true);
        await scanner.ScanAsync();
        await uow.CompleteAsync();
    }
}
