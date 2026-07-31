using System;
using System.Threading;
using System.Threading.Tasks;
using Intelligence.Settings;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Settings;
using Volo.Abp.Threading;

namespace Intelligence.Decisions;

/// <summary>
/// Common scheduling behavior for Intelligence scanner workers. The initial
/// period is loaded from ABP settings before the timer starts, and a saved
/// interval restarts the timer so the new schedule takes effect immediately.
/// </summary>
public abstract class SettingBasedScannerWorkerBase : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IntelligenceBackgroundJobScheduleNotifier _scheduleNotifier;
    private readonly string _settingName;
    private readonly int _defaultIntervalMinutes;
    private bool _subscribed;

    protected SettingBasedScannerWorkerBase(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IntelligenceBackgroundJobScheduleNotifier scheduleNotifier,
        string settingName,
        int defaultIntervalMinutes)
        : base(timer, serviceScopeFactory)
    {
        _scheduleNotifier = scheduleNotifier;
        _settingName = settingName;
        _defaultIntervalMinutes = defaultIntervalMinutes;
        ApplyInterval(defaultIntervalMinutes, restartTimer: false);
    }

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var settingProvider = scope.ServiceProvider.GetRequiredService<ISettingProvider>();
        var minutes = await settingProvider.GetAsync<int>(_settingName);
        ApplyInterval(minutes, restartTimer: false);

        if (!_subscribed)
        {
            _scheduleNotifier.IntervalChanged += OnIntervalChanged;
            _subscribed = true;
        }

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_subscribed)
        {
            _scheduleNotifier.IntervalChanged -= OnIntervalChanged;
            _subscribed = false;
        }

        await base.StopAsync(cancellationToken);
    }

    private void OnIntervalChanged(string settingName, int minutes)
    {
        if (!string.Equals(settingName, _settingName, StringComparison.Ordinal))
        {
            return;
        }

        ApplyInterval(minutes, restartTimer: true);
    }

    private void ApplyInterval(int minutes, bool restartTimer)
    {
        minutes = IntelligenceSettings.NormalizeInterval(minutes, _defaultIntervalMinutes);
        var period = checked((int)TimeSpan.FromMinutes(minutes).TotalMilliseconds);

        if (Timer.Period == period)
        {
            return;
        }

        if (restartTimer)
        {
            Timer.Stop(StartCancellationToken);
        }

        Timer.Period = period;

        if (restartTimer)
        {
            Timer.Start(StartCancellationToken);
        }
    }
}
