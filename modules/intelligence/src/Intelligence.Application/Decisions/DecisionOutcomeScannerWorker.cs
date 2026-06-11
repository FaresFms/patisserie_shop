using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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
/// Interval comes from configuration: "BackgroundJobs:DecisionOutcomeScanIntervalMinutes".
/// Default is 360 (6 hours) — outcomes are judged 48h after creation, so a few runs a
/// day is plenty.
/// </summary>
public class DecisionOutcomeScannerWorker : AsyncPeriodicBackgroundWorkerBase
{
    private const int DefaultIntervalMinutes = 360; // 6 hours

    public DecisionOutcomeScannerWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration)
        : base(timer, serviceScopeFactory)
    {
        var minutes = configuration.GetValue<int?>("BackgroundJobs:DecisionOutcomeScanIntervalMinutes")
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
        var scanner = sp.GetRequiredService<DecisionOutcomeScannerService>();

        using var uow = uowManager.Begin(requiresNew: true);
        await scanner.ScanAsync();
        await uow.CompleteAsync();
    }
}
