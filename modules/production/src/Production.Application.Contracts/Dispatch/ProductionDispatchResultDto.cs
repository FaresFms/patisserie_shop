using System;

namespace Production.Dispatch;

public class ProductionDispatchResultDto
{
    public Guid StockTransferId { get; set; }
    public string StockTransferReference { get; set; } = null!;
    public int DispatchedQuantity { get; set; }
    public int FulfilledRequestQuantity { get; set; }
}
