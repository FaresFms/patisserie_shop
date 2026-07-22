using System;

namespace Inventory.Stocktakes;

public class StocktakeReconciliationSummary
{
    public int CompletedSessionCount { get; set; }
    public int CountedLineCount { get; set; }
    public int DifferenceLineCount { get; set; }
    public int WriteOffLineCount { get; set; }
    public int ManualAdjustmentLineCount { get; set; }
    public int ShortageQuantity { get; set; }
    public int OverageQuantity { get; set; }
}

public class StocktakeProductVarianceAggregate
{
    public Guid ProductId { get; set; }
    public int SessionCount { get; set; }
    public int DifferenceLineCount { get; set; }
    public int ShortageQuantity { get; set; }
    public int OverageQuantity { get; set; }
    public int WriteOffQuantity { get; set; }
    public int UnrecordedSaleQuantity { get; set; }
}

public class StocktakeReasonAggregate
{
    public string Reason { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public int ShortageQuantity { get; set; }
    public int OverageQuantity { get; set; }
}
