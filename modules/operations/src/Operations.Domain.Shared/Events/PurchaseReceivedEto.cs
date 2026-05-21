using System;

namespace Operations.Events;

[Serializable]
public class PurchaseReceivedEto
{
    public Guid PurchaseOrderId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid DestBranchId { get; set; }
}
