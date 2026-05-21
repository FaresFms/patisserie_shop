using System;

namespace Operations.Events;

[Serializable]
public class SaleRecordedEto
{
    public Guid SaleId { get; set; }
    public Guid BranchId { get; set; }
}
