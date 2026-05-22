using System;
using System.Collections.Generic;

namespace Operations.PurchaseOrders;

public class AddPurchaseOrderItemDto
{
    public Guid ProductId { get; set; }
    public int OrderedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class UpdatePurchaseOrderItemDto
{
    public int OrderedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class ReceiveItemsDto
{
    public List<ReceiveLineDto> Lines { get; set; } = new();
}

public class ReceiveLineDto
{
    public Guid ItemId { get; set; }
    public int ReceivedQuantity { get; set; }
}
