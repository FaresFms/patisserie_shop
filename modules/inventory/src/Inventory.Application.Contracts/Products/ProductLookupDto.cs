using System;

namespace Inventory.Products;

public class ProductLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string SKU { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public decimal SalePrice { get; set; }
    public string Currency { get; set; } = "USD";
}
