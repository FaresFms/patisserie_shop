namespace Intelligence.Decisions;

/// <summary>
/// Typed projection returned by IDecisionLogRepository.GetSummaryAsync for the
/// dashboard's four header cards. Filtered server-side using the same accessible
/// branch set the list query uses.
/// </summary>
public class DecisionLogSummary
{
    public int TotalPending { get; init; }
    public int LowStockPending { get; init; }
    public int ExcessStockPending { get; init; }
    public int ResolvedToday { get; init; }
}
