using System;
using System.Collections.Generic;

namespace Operations.Events;

[Serializable]
public class TransferCompletedEto
{
    public Guid TransferId { get; set; }
    public Guid FromBranchId { get; set; }
    public Guid ToBranchId { get; set; }
    public List<TransferCompletedLineEto> Lines { get; set; } = new();
}

[Serializable]
public class TransferCompletedLineEto
{
    public Guid TransferItemId { get; set; }
    public Guid ProductId { get; set; }
    public int ShippedQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
}
