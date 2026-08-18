using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Operations;

/// <summary>
/// Compact serialization of the expiry-batch breakdown consumed when a transfer
/// item ships ("yyyy-MM-dd:qty:unitCost;..."). Legacy date/quantity segments remain
/// readable. The source branch's FEFO batch
/// consumption is only known at ship time; the receive step replays this breakdown
/// so the destination branch inherits the real expiry dates instead of restarting
/// the shelf life.
/// </summary>
public static class StockTransferBatchBreakdown
{
    public sealed record Line(DateTime ExpiryDate, int Quantity, decimal? UnitCost = null);

    /// <summary>Must match the max length configured for AppStockTransferItem.ShippedBatchBreakdown.</summary>
    public const int MaxLength = 1024;

    public static string? Format(IEnumerable<Line> lines)
    {
        var parts = lines
            .Where(l => l.Quantity > 0)
            .GroupBy(l => new { ExpiryDate = l.ExpiryDate.Date, l.UnitCost })
            .OrderBy(g => g.Key.ExpiryDate)
            .ThenBy(g => g.Key.UnitCost)
            .Select(g => g.Key.UnitCost.HasValue
                ? $"{g.Key.ExpiryDate:yyyy-MM-dd}:{g.Sum(l => l.Quantity)}:{g.Key.UnitCost.Value.ToString(CultureInfo.InvariantCulture)}"
                : $"{g.Key.ExpiryDate:yyyy-MM-dd}:{g.Sum(l => l.Quantity)}")
            .ToList();

        if (parts.Count == 0)
        {
            return null;
        }

        // Keep the earliest-expiry segments that fit the column; the tail is simply
        // not recorded, so the receive step tops it up as an untracked remainder —
        // quantity accounting stays exact, only expiry detail is lost.
        var builder = new System.Text.StringBuilder();
        foreach (var part in parts)
        {
            var extra = (builder.Length == 0 ? 0 : 1) + part.Length;
            if (builder.Length + extra > MaxLength)
            {
                break;
            }
            if (builder.Length > 0)
            {
                builder.Append(';');
            }
            builder.Append(part);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    /// <summary>Parses leniently — a malformed segment is skipped, never thrown on.</summary>
    public static List<Line> Parse(string? breakdown)
    {
        var result = new List<Line>();
        if (string.IsNullOrWhiteSpace(breakdown))
        {
            return result;
        }

        foreach (var part in breakdown.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = part.Split(':');
            if (fields.Length is < 2 or > 3)
            {
                continue;
            }

            if (DateTime.TryParseExact(
                    fields[0], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var expiry)
                && int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty)
                && qty > 0)
            {
                decimal? unitCost = null;
                if (fields.Length == 3
                    && decimal.TryParse(fields[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedCost)
                    && parsedCost >= 0m)
                {
                    unitCost = parsedCost;
                }
                result.Add(new Line(expiry, qty, unitCost));
            }
        }

        return result.OrderBy(l => l.ExpiryDate).ToList();
    }
}
