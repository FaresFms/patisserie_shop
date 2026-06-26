using System;

namespace Production.Orders;

public class CompleteProductionOrderDto
{
    public int ActualOutputQuantity { get; set; }
    public int AcceptedQuantity { get; set; }
    public int RejectedQuantity { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? WasteReason { get; set; }
    public string? Notes { get; set; }
}
