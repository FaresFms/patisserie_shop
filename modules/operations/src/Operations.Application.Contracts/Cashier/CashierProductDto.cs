using System;

namespace Operations.Cashier;

/// <summary>
/// A sellable product tile for the cashier POS. CRITICAL: this DTO deliberately exposes
/// neither CostPrice nor purchasing details. QuantityOnHand is the current branch stock
/// needed to keep the POS cart from exceeding available inventory; the server still
/// re-checks stock when recording the sale.
/// </summary>
public class CashierProductDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = null!;
    public string SKU { get; set; } = null!;
    public string? Description { get; set; }
    public decimal SalePrice { get; set; }
    public string? ImageUrl { get; set; }
    public int QuantityOnHand { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsOutOfStock { get; set; }
}
