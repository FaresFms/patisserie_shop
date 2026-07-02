using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Operations;

/// <summary>
/// Compact serialization of the expiry-batch breakdown consumed when a transfer
/// item ships ("yyyy-MM-dd:qty;yyyy-MM-dd:qty"). The source branch's FEFO batch
/// consumption is only known at ship time; the receive step replays this breakdown
/// so the destination branch inherits the real expiry dates instead of restarting
/// the shelf life.
/// </summary>
public static class StockTransferBatchBreakdown
{
    public sealed record Line(DateTime ExpiryDate, int Quantity);

    /// <summary>Must match the max length configured for AppStockTransferItem.ShippedBatchBreakdown.</summary>
    public const int MaxLength = 1024;

    public static string? Format(IEnumerable<Line> lines)
    {
        var parts = lines
            .Where(l => l.Quantity > 0)
            .GroupBy(l => l.ExpiryDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key:yyyy-MM-dd}:{g.Sum(l => l.Quantity)}")
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
            var separator = part.LastIndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            if (DateTime.TryParseExact(
                    part[..separator], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var expiry)
                && int.TryParse(part[(separator + 1)..], out var qty)
                && qty > 0)
            {
                result.Add(new Line(expiry, qty));
            }
        }

        return result.OrderBy(l => l.ExpiryDate).ToList();
    }
}
