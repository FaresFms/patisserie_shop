using System;

namespace Inventory.Events;

[Serializable]
public class StockChangedEto
{
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public int OldQty { get; set; }
    public int NewQty { get; set; }
}
