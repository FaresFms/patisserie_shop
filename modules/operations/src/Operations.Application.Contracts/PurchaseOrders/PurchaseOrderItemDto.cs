using System;

namespace Operations.PurchaseOrders;

public class PurchaseOrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductSKU { get; set; } = null!;
    public string ProductUnit { get; set; } = null!;
    public int OrderedQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public bool IsFullyReceived => ReceivedQuantity >= OrderedQuantity;
}
