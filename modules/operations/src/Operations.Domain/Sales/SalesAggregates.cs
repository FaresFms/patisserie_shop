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
