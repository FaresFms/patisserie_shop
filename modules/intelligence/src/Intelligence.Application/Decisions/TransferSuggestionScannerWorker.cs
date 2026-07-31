using System.Threading.Tasks;
using Intelligence.Settings;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace Intelligence.Decisions;

/// <summary>
/// Periodic worker that drives <see cref="TransferSuggestionScannerService"/>. As with the
/// DeadStock worker, the scan logic lives in the service; the worker owns only the schedule
/// and the unit-of-work boundary.
///
/// Interval comes from the ABP setting
/// <see cref="IntelligenceSettings.TransferSuggestionScanIntervalMinutes"/>.
/// </summary>
public class TransferSuggestionScannerWorker : SettingBasedScannerWorkerBase
{
    public TransferSuggestionScannerWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IntelligenceBackgroundJobScheduleNotifier scheduleNotifier)
        : base(
            timer,
            serviceScopeFactory,
            scheduleNotifier,
            IntelligenceSettings.TransferSuggestionScanIntervalMinutes,
            IntelligenceSettings.TransferSuggestionDefaultIntervalMinutes)
    {
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var sp = workerContext.ServiceProvider;
        var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
        var scanner = sp.GetRequiredService<TransferSuggestionScannerService>();

        using var uow = uowManager.Begin(requiresNew: true);
        await scanner.ScanAsync();
        await uow.CompleteAsync();
    }
}
