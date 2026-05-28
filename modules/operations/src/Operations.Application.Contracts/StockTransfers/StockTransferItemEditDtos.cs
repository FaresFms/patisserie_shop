using System;
using System.Collections.Generic;

namespace Operations.StockTransfers;

public class AddStockTransferItemDto
{
    public Guid ProductId { get; set; }
    public int RequestedQuantity { get; set; }
}

public class UpdateApprovedQuantityDto
{
    public int ApprovedQuantity { get; set; }
}

public class CompleteStockTransferDto
{
    public List<CompleteTransferLineDto> Lines { get; set; } = new();
}

public class CompleteTransferLineDto
{
    public Guid ItemId { get; set; }
    public int TransferredQuantity { get; set; }
}
