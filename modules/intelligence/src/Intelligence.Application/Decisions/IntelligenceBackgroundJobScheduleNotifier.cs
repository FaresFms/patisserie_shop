using System;

namespace Intelligence.Decisions;

/// <summary>
/// Updates the timers of the running Intelligence workers immediately after the
/// administrator saves a new interval. Persisted values remain in ABP settings;
/// this singleton only bridges a successful settings update to the live workers.
/// </summary>
public sealed class IntelligenceBackgroundJobScheduleNotifier
{
    public event Action<string, int>? IntervalChanged;

    public void Notify(string settingName, int minutes)
        => IntervalChanged?.Invoke(settingName, minutes);
}
