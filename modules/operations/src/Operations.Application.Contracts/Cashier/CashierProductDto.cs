using System;

namespace Operations.Cashier;

/// <summary>
/// A sellable product tile for the cashier POS. CRITICAL: this DTO deliberately exposes
/// neither CostPrice nor QuantityOnHand — the cashier sees the sale price and coarse
/// availability flags only. IsLowStock / IsOutOfStock are derived from the branch
/// inventory row server-side; the numeric quantity never crosses the wire.
/// </summary>
public class CashierProductDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = null!;
    public string SKU { get; set; } = null!;
    public decimal SalePrice { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsOutOfStock { get; set; }
}
