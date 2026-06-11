using System;
using System.Collections.Generic;

namespace patisserie_shop.Analytics;

public class SalesAnalyticsDto
{
    /// <summary>The normalized window actually used (7, 30 or 90).</summary>
    public int Days { get; set; }

    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // ── Headline KPIs ──
    public decimal TotalRevenue { get; set; }
    public int TotalSales { get; set; }
    public decimal AvgSaleValue { get; set; }
    public int DistinctProductsSold { get; set; }

    /// <summary>One point per day in the window, zero-filled for days without sales.</summary>
    public List<DailySalesPointDto> DailySeries { get; set; } = new();

    /// <summary>Revenue + sale count per branch, ordered by revenue descending.</summary>
    public List<BranchSalesSliceDto> Branches { get; set; } = new();

    /// <summary>Revenue per product category, ordered by revenue descending.</summary>
    public List<CategorySalesSliceDto> CategoryMix { get; set; } = new();

    /// <summary>Top 10 products by revenue in the window.</summary>
    public List<ProductSalesRowDto> TopMovers { get; set; } = new();

    /// <summary>
    /// Bottom 10 products by revenue among products that sold at least once
    /// in the window (revenue ascending).
    /// </summary>
    public List<ProductSalesRowDto> SlowMovers { get; set; } = new();
}

public class DailySalesPointDto
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public int SaleCount { get; set; }
}

public class BranchSalesSliceDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int SaleCount { get; set; }
}

public class CategorySalesSliceDto
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class ProductSalesRowDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}
