using System;
using Volo.Abp.Domain.Entities;

namespace Intelligence.Entities;

/// <summary>
/// Nightly-computed sales-velocity read model per product×branch. Rebuilt by the
/// VelocityScannerService from the trailing 7/30-day sale aggregates; AbcClass is the
/// product's global ABC classification (by 30-day revenue share) copied onto every
/// branch row. Not audited and not soft-deleted — it is derived data, upserted in
/// place on every scan. Unique per (ProductId, BranchId).
/// </summary>
public class AppProductVelocity : BasicAggregateRoot<Guid>
{
    public Guid ProductId { get; private set; }
    public Guid BranchId { get; private set; }
    public decimal AvgDailySales7 { get; private set; }
    public decimal AvgDailySales30 { get; private set; }
    public int QuantitySold30 { get; private set; }
    public decimal Revenue30 { get; private set; }

    /// <summary>"A", "B" or "C" (default "C").</summary>
    public string AbcClass { get; private set; } = "C";

    public DateTime ComputedAtUtc { get; private set; }

    /// <summary>
    /// Per-weekday demand indices: (avg units sold on that weekday) ÷ (overall avg
    /// daily units) over the trailing 30 days. 1.0 = flat / no weekday pattern.
    /// Clamped to [0, 5]. Sunday-first to match <see cref="DayOfWeek"/>.
    /// </summary>
    public decimal WeekdayIndexSun { get; private set; } = 1.0m;
    public decimal WeekdayIndexMon { get; private set; } = 1.0m;
    public decimal WeekdayIndexTue { get; private set; } = 1.0m;
    public decimal WeekdayIndexWed { get; private set; } = 1.0m;
    public decimal WeekdayIndexThu { get; private set; } = 1.0m;
    public decimal WeekdayIndexFri { get; private set; } = 1.0m;
    public decimal WeekdayIndexSat { get; private set; } = 1.0m;

    protected AppProductVelocity() { }

    public AppProductVelocity(Guid id, Guid productId, Guid branchId)
        : base(id)
    {
        ProductId = productId;
        BranchId = branchId;
        AbcClass = "C";
    }

    public void UpdateMetrics(
        decimal avgDaily7,
        decimal avgDaily30,
        int qtySold30,
        decimal revenue30,
        string abcClass,
        DateTime computedAtUtc)
    {
        if (avgDaily7 < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(avgDaily7), avgDaily7, "Velocity metrics cannot be negative.");
        }
        if (avgDaily30 < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(avgDaily30), avgDaily30, "Velocity metrics cannot be negative.");
        }
        if (qtySold30 < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(qtySold30), qtySold30, "Velocity metrics cannot be negative.");
        }
        if (revenue30 < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revenue30), revenue30, "Velocity metrics cannot be negative.");
        }
        if (abcClass != "A" && abcClass != "B" && abcClass != "C")
        {
            throw new ArgumentException($"AbcClass must be \"A\", \"B\" or \"C\" but was \"{abcClass}\".", nameof(abcClass));
        }

        AvgDailySales7 = avgDaily7;
        AvgDailySales30 = avgDaily30;
        QuantitySold30 = qtySold30;
        Revenue30 = revenue30;
        AbcClass = abcClass;
        ComputedAtUtc = computedAtUtc;
    }

    /// <summary>
    /// Sets the 7 per-weekday demand indices, Sunday-first (index 0 = Sunday …
    /// index 6 = Saturday, the <see cref="DayOfWeek"/> convention). Each value
    /// must be non-negative; values above 5 are clamped to 5 and everything is
    /// rounded to 2 decimals (the column precision).
    /// </summary>
    public void SetWeekdayIndices(decimal[] indices)
    {
        if (indices == null)
        {
            throw new ArgumentNullException(nameof(indices));
        }
        if (indices.Length != 7)
        {
            throw new ArgumentException(
                $"Exactly 7 weekday indices (Sunday-first) are required but {indices.Length} were supplied.",
                nameof(indices));
        }

        var sanitized = new decimal[7];
        for (var i = 0; i < 7; i++)
        {
            if (indices[i] < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(indices), indices[i], "Weekday demand indices cannot be negative.");
            }
            sanitized[i] = Math.Round(Math.Min(indices[i], 5m), 2, MidpointRounding.AwayFromZero);
        }

        WeekdayIndexSun = sanitized[0];
        WeekdayIndexMon = sanitized[1];
        WeekdayIndexTue = sanitized[2];
        WeekdayIndexWed = sanitized[3];
        WeekdayIndexThu = sanitized[4];
        WeekdayIndexFri = sanitized[5];
        WeekdayIndexSat = sanitized[6];
    }

    /// <summary>Indices as a Sunday-first array (index = (int)DayOfWeek).</summary>
    public decimal[] GetWeekdayIndices() =>
    [
        WeekdayIndexSun,
        WeekdayIndexMon,
        WeekdayIndexTue,
        WeekdayIndexWed,
        WeekdayIndexThu,
        WeekdayIndexFri,
        WeekdayIndexSat
    ];
}
