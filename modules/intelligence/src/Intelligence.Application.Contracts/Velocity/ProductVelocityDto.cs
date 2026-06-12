using System;
using Volo.Abp.Application.Dtos;

namespace Intelligence.Velocity;

public class ProductVelocityDto : EntityDto<Guid>
{
    public Guid ProductId { get; set; }
    public Guid BranchId { get; set; }

    public decimal AvgDailySales7 { get; set; }
    public decimal AvgDailySales30 { get; set; }
    public int QuantitySold30 { get; set; }
    public decimal Revenue30 { get; set; }

    /// <summary>"A", "B" or "C" — global per-product class by 30-day revenue share.</summary>
    public string AbcClass { get; set; } = "C";

    public DateTime ComputedAtUtc { get; set; }

    /// <summary>Resolved by the repository join from the product.</summary>
    public string? ProductName { get; set; }

    /// <summary>Resolved by the repository join from the product.</summary>
    public string? ProductSku { get; set; }

    /// <summary>Resolved by the repository join from the branch.</summary>
    public string? BranchName { get; set; }

    /// <summary>QuantityOnHand of the matching branch-inventory row.</summary>
    public int CurrentStock { get; set; }

    /// <summary>
    /// Days until the weekday-indexed forecast walk (starting tomorrow) depletes
    /// CurrentStock; falls back to CurrentStock ÷ AvgDailySales30 when there is no
    /// weekday pattern. Null when the 30-day velocity is zero.
    /// </summary>
    public decimal? DaysOfCover { get; set; }

    /// <summary>
    /// Forecast units for the next 7 calendar days (starting tomorrow):
    /// Σ AvgDailySales30 × weekday index of each day. Null when velocity is zero.
    /// </summary>
    public decimal? Next7DaysForecast { get; set; }

    /// <summary>
    /// Per-weekday demand indices, Sunday-first (1.0 = flat / no pattern).
    /// Mapped 1:1 from the velocity row; backs the per-day forecast tooltip.
    /// </summary>
    public decimal WeekdayIndexSun { get; set; } = 1.0m;
    public decimal WeekdayIndexMon { get; set; } = 1.0m;
    public decimal WeekdayIndexTue { get; set; } = 1.0m;
    public decimal WeekdayIndexWed { get; set; } = 1.0m;
    public decimal WeekdayIndexThu { get; set; } = 1.0m;
    public decimal WeekdayIndexFri { get; set; } = 1.0m;
    public decimal WeekdayIndexSat { get; set; } = 1.0m;
}
