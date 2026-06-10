using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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
/// Interval comes from configuration: "BackgroundJobs:TransferSuggestionScanIntervalMinutes".
/// Development/demo uses 5; production should use 1440 (24 hours).
/// </summary>
public class TransferSuggestionScannerWorker : AsyncPeriodicBackgroundWorkerBase
{
    private const int DefaultIntervalMinutes = 1440; // 24 hours (production default)

    public TransferSuggestionScannerWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration)
        : base(timer, serviceScopeFactory)
    {
        var minutes = configuration.GetValue<int?>("BackgroundJobs:TransferSuggestionScanIntervalMinutes")
                      ?? DefaultIntervalMinutes;
        if (minutes <= 0)
        {
            minutes = DefaultIntervalMinutes;
        }

        Timer.Period = (int)TimeSpan.FromMinutes(minutes).TotalMilliseconds;
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
