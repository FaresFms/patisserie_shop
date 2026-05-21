using System;

namespace Operations.Events;

[Serializable]
public class TransferCompletedEto
{
    public Guid TransferId { get; set; }
    public Guid FromBranchId { get; set; }
    public Guid ToBranchId { get; set; }
}
