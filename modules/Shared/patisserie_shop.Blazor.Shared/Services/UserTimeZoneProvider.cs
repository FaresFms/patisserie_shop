using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace patisserie_shop.Blazor.Shared.Services;

public interface IUserTimeZoneProvider
{
    Task EnsureLoadedAsync();
    DateTime ToUserTime(DateTime value);
    string FormatDate(DateTime value, string format = "MMM dd yyyy");
    string FormatTime(DateTime value, string format = "HH:mm");
    string Format(DateTime value, string format);
    string Format(DateTime? value, string format, string fallback = "—");
}

public class UserTimeZoneProvider : IUserTimeZoneProvider, IScopedDependency
{
    private readonly ISettingProvider _settingProvider;
    private bool _loaded;
    private TimeZoneInfo _timeZone = TimeZoneInfo.Utc;

    private const string AbpTimeZoneSettingName = "Abp.Timing.TimeZone";

    public UserTimeZoneProvider(ISettingProvider settingProvider)
    {
        _settingProvider = settingProvider;
    }

    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
            return;

        var timeZoneId = await _settingProvider.GetOrNullAsync(AbpTimeZoneSettingName);
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch
            {
                _timeZone = TimeZoneInfo.Utc;
            }
        }

        _loaded = true;
    }

    public DateTime ToUserTime(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();

        return TimeZoneInfo.ConvertTimeFromUtc(utc, _timeZone);
    }

    public string Format(DateTime value, string format)
    {
        return ToUserTime(value).ToString(format);
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
}
