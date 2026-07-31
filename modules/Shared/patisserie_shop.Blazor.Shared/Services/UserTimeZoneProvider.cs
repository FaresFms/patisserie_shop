using System;
using System.Globalization;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace patisserie_shop.Blazor.Shared.Services;

public interface IUserTimeZoneProvider
{
    Task EnsureLoadedAsync();
    Task<bool> SetBrowserTimeZoneAsync(string? timeZoneId);
    DateTime ToUserTime(DateTime value);
    DateTime UserNow { get; }
    string FormatDate(DateTime value, string format = "MMM dd yyyy");
    string FormatTime(DateTime value, string format = "HH:mm");
    string Format(DateTime value, string format);
    string Format(DateTime? value, string format, string fallback = "—");
}

public class UserTimeZoneProvider : IUserTimeZoneProvider, IScopedDependency
{
    private readonly ISettingProvider _settingProvider;
    private bool _loaded;
    private bool _browserApplied;
    private TimeZoneInfo _timeZone = TimeZoneInfo.Utc;

    private const string AbpTimeZoneSettingName = "Abp.Timing.TimeZone";

    public UserTimeZoneProvider(ISettingProvider settingProvider)
    {
        _settingProvider = settingProvider;
    }

    public async Task EnsureLoadedAsync()
    {
        // Browser detection always has precedence over the platform fallback.
        if (_loaded || _browserApplied)
            return;

        string? timeZoneId;
        try
        {
            timeZoneId = await _settingProvider.GetOrNullAsync(AbpTimeZoneSettingName);
        }
        catch
        {
            // Timezone display must remain available even if settings cannot be
            // read during startup; UTC is the final safe fallback.
            timeZoneId = null;
        }
        var configuredZone = ResolveTimeZone(timeZoneId);
        if (configuredZone is not null)
        {
            _timeZone = configuredZone;
        }

        _loaded = true;
    }

    public Task<bool> SetBrowserTimeZoneAsync(string? timeZoneId)
    {
        var browserZone = ResolveTimeZone(timeZoneId);
        if (browserZone is null)
        {
            return Task.FromResult(false);
        }

        var changed = !_browserApplied || browserZone.Id != _timeZone.Id;
        _timeZone = browserZone;
        _browserApplied = true;
        _loaded = true;

        return Task.FromResult(changed);
    }

    public DateTime ToUserTime(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();

        return TimeZoneInfo.ConvertTimeFromUtc(utc, _timeZone);
    }

    public DateTime UserNow => ToUserTime(DateTime.UtcNow);

    public string Format(DateTime value, string format)
    {
        return ToUserTime(value).ToString(format, CultureInfo.CurrentCulture);
    }

    public string FormatDate(DateTime value, string format = "MMM dd yyyy")
    {
        return Format(value, format);
    }

    public string FormatTime(DateTime value, string format = "HH:mm")
    {
        return Format(value, format);
    }

    public string Format(DateTime? value, string format, string fallback = "—")
    {
        return value.HasValue ? Format(value.Value, format) : fallback;
    }

    private static TimeZoneInfo? ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return ResolveAlternateTimeZoneId(id);
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    private static TimeZoneInfo? ResolveAlternateTimeZoneId(string id)
    {
        string? alternateId = null;
        var converted = OperatingSystem.IsWindows()
            ? TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out alternateId)
            : TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out alternateId);

        if (!converted || string.IsNullOrWhiteSpace(alternateId))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(alternateId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
