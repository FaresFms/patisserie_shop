using System;
using System.Collections.Generic;

namespace patisserie_shop.Analytics;

public class WasteAnalyticsDto
{
    /// <summary>The normalized window actually used (30 or 90).</summary>
    public int Days { get; set; }

    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // ── Headline KPIs ──

    /// <summary>Sum of write-off cost (units × product CostPrice) in the window.</summary>
    public decimal TotalWasteCost { get; set; }

    /// <summary>Total units written off in the window.</summary>
    public int TotalUnitsWrittenOff { get; set; }

    /// <summary>
    /// Waste cost as a percentage of sales revenue over the same window and branch
    /// scope (0 when there were no sales).
    /// </summary>
    public decimal WasteToSalesPercent { get; set; }

    /// <summary>The product with the highest write-off cost, or null when no waste.</summary>
    public string? TopWastedProductName { get; set; }

    /// <summary>One point per week in the window, zero-filled for weeks without waste.</summary>
    public List<WeeklyWastePointDto> WeeklySeries { get; set; } = new();

    /// <summary>Write-off cost + units per branch, ordered by cost descending.</summary>
    public List<BranchWasteSliceDto> Branches { get; set; } = new();

    /// <summary>Top 10 products by write-off cost in the window.</summary>
    public List<ProductWasteRowDto> TopProducts { get; set; } = new();
}

public class WeeklyWastePointDto
{
    /// <summary>First day of the bucket (window start + 7-day steps, UTC date).</summary>
    public DateTime WeekStart { get; set; }

    public decimal Cost { get; set; }
    public int Units { get; set; }
}

public class BranchWasteSliceDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public int Units { get; set; }
}

public class ProductWasteRowDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public int Units { get; set; }
    public decimal Cost { get; set; }
}
