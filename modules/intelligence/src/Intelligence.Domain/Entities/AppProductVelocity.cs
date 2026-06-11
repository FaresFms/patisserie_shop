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
}
