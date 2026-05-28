using System;

namespace Operations.StockTransfers;

public class StockTransferItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductSKU { get; set; } = null!;
    public string ProductUnit { get; set; } = null!;
    public int RequestedQuantity { get; set; }
    public int? ApprovedQuantity { get; set; }
    public int? TransferredQuantity { get; set; }
}
