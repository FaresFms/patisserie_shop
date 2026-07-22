using System;

namespace Inventory.BranchInventory;

public class StocktakeResultDto
{
    public Guid ReferenceId { get; set; }
    public int CountedLineCount { get; set; }
    public int AdjustedLineCount { get; set; }
    public int MatchedLineCount { get; set; }
    public int WriteOffLineCount { get; set; }
    public int ManualAdjustmentLineCount { get; set; }
    public int ShortageQuantity { get; set; }
    public int OverageQuantity { get; set; }
}
