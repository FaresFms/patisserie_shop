using System;

namespace Operations.StockTransfers;

/// <summary>Sellable/transferable product at the source branch, with its current stock.</summary>
public class StockTransferProductLookupDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = null!;
    public string SKU { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public int QuantityOnHand { get; set; }
}
