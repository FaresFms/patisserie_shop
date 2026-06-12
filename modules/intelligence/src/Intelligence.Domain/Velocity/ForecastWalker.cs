using System;
using System.Collections.Generic;

namespace Intelligence.Velocity;

/// <summary>
/// Pure, deterministic weekday-indexed demand forecast. Daily demand for a given
/// calendar day is AvgDailySales30 × the weekday index of that day's
/// <see cref="DayOfWeek"/> (Sunday-first array, index = (int)DayOfWeek). Shared by
/// the nightly VelocityScannerService sweep, the real-time DecisionMakerService and
/// the velocity read model — no ML, plain arithmetic.
/// </summary>
public static class ForecastWalker
{
    /// <summary>
    /// Forecast demand for a single day: <paramref name="avgDaily"/> × the index of
    /// <paramref name="day"/>. Indices must be a Sunday-first array of 7 values.
    /// </summary>
    public static decimal DailyDemand(decimal avgDaily, IReadOnlyList<decimal> weekdayIndices, DayOfWeek day)
    {
        EnsureSevenIndices(weekdayIndices);
        return avgDaily * weekdayIndices[(int)day];
    }

    /// <summary>
    /// Walks the forecast day by day, subtracting each day's weekday-indexed demand
    /// from <paramref name="onHand"/>, and returns the 1-based day number on which
    /// stock depletes (≤ 0). Day 1 has weekday <paramref name="startDay"/> — pass
    /// tomorrow's <see cref="DayOfWeek"/> to start the walk tomorrow. Returns null
    /// when stock survives the whole <paramref name="horizonDays"/>-day horizon or
    /// when <paramref name="avgDaily"/> is not positive (no demand signal); returns
    /// 0 when already out of stock.
    /// </summary>
    public static int? DaysUntilDepletion(
        int onHand,
        decimal avgDaily,
        IReadOnlyList<decimal> weekdayIndices,
        DayOfWeek startDay,
        int horizonDays)
    {
        EnsureSevenIndices(weekdayIndices);

        if (avgDaily <= 0)
        {
            return null;
        }
        if (onHand <= 0)
        {
            return 0;
        }

        decimal remaining = onHand;
        for (var day = 1; day <= horizonDays; day++)
        {
            var weekday = (int)((((int)startDay) + day - 1) % 7);
            remaining -= avgDaily * weekdayIndices[weekday];
            if (remaining <= 0)
            {
                return day;
            }
        }

        return null;
    }

    /// <summary>
    /// True when the indices carry no usable weekday pattern — all exactly 1.0 (flat)
    /// or all zero (unmigrated / uncomputed row). Callers should fall back to the
    /// plain QuantityOnHand ÷ AvgDailySales30 division in that case.
    /// </summary>
    public static bool IsFlat(IReadOnlyList<decimal> weekdayIndices)
    {
        EnsureSevenIndices(weekdayIndices);

        var allOne = true;
        var allZero = true;
        for (var i = 0; i < 7; i++)
        {
            if (weekdayIndices[i] != 1m) allOne = false;
            if (weekdayIndices[i] != 0m) allZero = false;
        }
        return allOne || allZero;
    }

    /// <summary>
    /// Human-readable note on the weekday weighting for decision-log reasoning,
    /// e.g. "busiest Saturday ×1.80, quietest Monday ×0.40". Deterministic: ties
    /// resolve to the earliest day (Sunday-first).
    /// </summary>
    public static string DescribeWeighting(IReadOnlyList<decimal> weekdayIndices)
    {
        EnsureSevenIndices(weekdayIndices);

        var busiest = 0;
        var quietest = 0;
        for (var i = 1; i < 7; i++)
        {
            if (weekdayIndices[i] > weekdayIndices[busiest]) busiest = i;
            if (weekdayIndices[i] < weekdayIndices[quietest]) quietest = i;
        }

        return $"busiest {(DayOfWeek)busiest} ×{weekdayIndices[busiest]:0.00}, " +
               $"quietest {(DayOfWeek)quietest} ×{weekdayIndices[quietest]:0.00}";
    }

    private static void EnsureSevenIndices(IReadOnlyList<decimal> weekdayIndices)
    {
        if (weekdayIndices == null)
        {
            throw new ArgumentNullException(nameof(weekdayIndices));
        }
        if (weekdayIndices.Count != 7)
        {
            throw new ArgumentException(
                $"Exactly 7 weekday indices (Sunday-first) are required but {weekdayIndices.Count} were supplied.",
                nameof(weekdayIndices));
        }
    }
}
