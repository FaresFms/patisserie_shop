using System;

namespace Operations.Cashier;

/// <summary>
/// A recent sale rung by the current cashier, for the POS "recent sales" strip.
/// CanVoid is true when the sale is not yet voided and still within the void window.
/// </summary>
public class RecentSaleDto
{
    public Guid SaleId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public DateTime SaleDate { get; set; }
    public decimal Total { get; set; }
    public int ItemCount { get; set; }
    public bool IsVoided { get; set; }
    public bool CanVoid { get; set; }
}
