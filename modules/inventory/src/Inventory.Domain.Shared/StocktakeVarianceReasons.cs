using System;

namespace Inventory;

/// <summary>
/// Stable reason codes stored with quick-stocktake movements. They remain language
/// neutral in the ledger and are localized when rendered in the UI.
/// </summary>
public static class StocktakeVarianceReasons
{
    public const string Damaged = "Damaged";
    public const string Expired = "Expired";
    public const string Waste = "Waste";
    public const string Complimentary = "Complimentary";
    public const string UnrecordedSale = "UnrecordedSale";
    public const string UnrecordedReceipt = "UnrecordedReceipt";
    public const string CountingError = "CountingError";
    public const string Other = "Other";

    public static readonly string[] All =
    {
        Damaged,
        Expired,
        Waste,
        Complimentary,
        UnrecordedSale,
        UnrecordedReceipt,
        CountingError,
        Other
    };

    public static bool IsValid(string? reason)
        => reason != null && Array.IndexOf(All, reason) >= 0;

    public static bool IsWriteOff(string reason)
        => reason is Damaged or Expired or Waste;

    public static bool IsAllowedForDifference(string? reason, int difference)
    {
        if (!IsValid(reason) || difference == 0) return false;

        return difference < 0
            ? reason != UnrecordedReceipt
            : reason is UnrecordedReceipt or CountingError or Other;
    }
}

/// <summary>
/// Compact, parseable encoding for the existing AppStockMovement.Notes column.
/// A migration is intentionally unnecessary for quick stocktake reasons.
/// </summary>
public static class StocktakeMovementNote
{
    private const string Prefix = "[StocktakeReason=";
    private const string Suffix = "] ";

    public static string Format(string reason, string notes, string? reasonNotes)
    {
        var detail = notes.Trim();
        if (!string.IsNullOrWhiteSpace(reasonNotes))
        {
            detail += " — " + reasonNotes.Trim();
        }

        return $"{Prefix}{reason}{Suffix}{detail}";
    }

    public static bool TryParse(string? value, out string reason, out string text)
    {
        reason = string.Empty;
        text = value ?? string.Empty;
        if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var suffixIndex = value.IndexOf(Suffix, Prefix.Length, StringComparison.Ordinal);
        if (suffixIndex < 0)
        {
            return false;
        }

        reason = value.Substring(Prefix.Length, suffixIndex - Prefix.Length);
        if (!StocktakeVarianceReasons.IsValid(reason))
        {
            reason = string.Empty;
            return false;
        }

        text = value[(suffixIndex + Suffix.Length)..];
        return true;
    }
}
