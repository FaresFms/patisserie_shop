using System;
using System.Globalization;

namespace Intelligence.Localization;

/// <summary>
/// Keeps generated database text in the shop's Arabic content language, regardless
/// of the UI culture or the culture inherited by a background worker.
/// </summary>
public static class PersistedContentCulture
{
    public const string ArabicCultureName = "ar-SY";

    public static IDisposable UseArabic()
        => new CultureScope(CultureInfo.GetCultureInfo(ArabicCultureName));

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture;
        private readonly CultureInfo _previousUiCulture;
        private bool _disposed;

        public CultureScope(CultureInfo culture)
        {
            _previousCulture = CultureInfo.CurrentCulture;
            _previousUiCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUiCulture;
            _disposed = true;
        }
    }
}
