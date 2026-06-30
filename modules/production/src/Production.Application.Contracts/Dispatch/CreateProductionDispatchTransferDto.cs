using System;

namespace Production.Dispatch;

public class CreateProductionDispatchTransferDto
{
    public Guid ProductionOrderId { get; set; }
    public Guid DestinationBranchId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}
