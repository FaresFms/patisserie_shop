namespace Intelligence.Decisions;

public class DecisionLogSummaryDto
{
    public int TotalPending { get; set; }
    public int LowStockPending { get; set; }
    public int ExcessStockPending { get; set; }
    public int ResolvedToday { get; set; }
}
