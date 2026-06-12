using System;

namespace Operations.Sales;

public class DailySaleAggregate
{
    public DateTime Date { get; set; }
    public decimal TotalAmount { get; set; }
    public int SaleCount { get; set; }
}

public class ProductSalesAggregate
{
    public Guid ProductId { get; set; }
    public int TotalQuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class BranchSalesAggregate
{
    public Guid BranchId { get; set; }
    public decimal TotalAmount { get; set; }
    public int SaleCount { get; set; }
}

/// <summary>
/// Units sold per product×branch×day-of-week over a window. DayOfWeek follows the
/// .NET <see cref="System.DayOfWeek"/> convention (0 = Sunday … 6 = Saturday).
/// Backs the weekday demand-index computation in the Intelligence module.
/// </summary>
public class ProductBranchWeekdaySalesAggregate
{
    public Guid ProductId { get; set; }
    public Guid BranchId { get; set; }

    /// <summary>0 = Sunday … 6 = Saturday (matches <see cref="System.DayOfWeek"/>).</summary>
    public int DayOfWeek { get; set; }

    public int QuantitySold { get; set; }
}

/// <summary>
/// Per product-per-branch sales totals over the trailing 7- and 30-day windows.
/// Backs the nightly velocity computation in the Intelligence module.
/// </summary>
public class ProductBranchSalesAggregate
{
    public Guid ProductId { get; set; }
    public Guid BranchId { get; set; }

    /// <summary>Units sold where SaleDate ≥ the 7-day window start.</summary>
    public int QuantitySold7 { get; set; }

    /// <summary>Units sold where SaleDate ≥ the 30-day window start.</summary>
    public int QuantitySold30 { get; set; }

    /// <summary>Revenue (qty × unit price) over the 30-day window.</summary>
    public decimal Revenue30 { get; set; }
}
