using System;

namespace patisserie_shop.Analytics;

public class GetWasteAnalyticsInput
{
    /// <summary>
    /// Trailing window length in days. Valid values are 30 and 90;
    /// anything else is clamped server-side to the nearest valid value.
    /// </summary>
    public int Days { get; set; } = 30;

    /// <summary>
    /// Optional narrowing to a single branch. Always intersected with the
    /// caller's accessible-branch scope — a branch manager asking for a
    /// branch they don't manage simply gets an empty result.
    /// </summary>
    public Guid? BranchId { get; set; }
}
