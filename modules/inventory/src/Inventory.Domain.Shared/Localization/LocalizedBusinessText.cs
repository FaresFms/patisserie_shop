using System;
using System.Globalization;

namespace Inventory.Localization;

/// <summary>
/// Selects the user-facing value of bilingual master data for the current UI culture.
/// Arabic is used for every Arabic culture (ar, ar-SY, ...); all other cultures use English.
/// A missing translation safely falls back to the other language.
/// </summary>
public static class LocalizedBusinessText
{
    public static bool IsArabic =>
        string.Equals(
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            "ar",
            StringComparison.OrdinalIgnoreCase);

    public static string Select(string? arabic, string? english)
    {
        var primary = IsArabic ? arabic : english;
        var fallback = IsArabic ? english : arabic;
        return !string.IsNullOrWhiteSpace(primary)
            ? primary
            : fallback ?? string.Empty;
    }
}
