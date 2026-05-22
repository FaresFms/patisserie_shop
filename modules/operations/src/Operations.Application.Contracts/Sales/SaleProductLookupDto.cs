using System;

namespace Operations.Sales;

/// <summary>Product available for sale at a specific branch, with live stock.</summary>
public class SaleProductLookupDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = null!;
    public string SKU { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public decimal SalePrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int QuantityOnHand { get; set; }
}
