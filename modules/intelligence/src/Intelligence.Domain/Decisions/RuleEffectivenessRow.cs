using System;

namespace Intelligence.Decisions;

/// <summary>
/// Typed read-model returned by IDecisionLogRepository: per-rule aggregate over ALL
/// decision logs (status workflow counts + 48h outcome counts) backing the
/// effectiveness column on the Inventory Rules page. Living next to the repository
/// interface (Domain) per CLAUDE.md: no anonymous types or tuples crossing the
/// repository boundary.
/// </summary>
public class RuleEffectivenessRow
{
    public Guid RuleId { get; init; }

    public int TotalDecisions { get; init; }

    // Status workflow counts.
    public int Pending { get; init; }
    public int Acknowledged { get; init; }
    public int Dismissed { get; init; }
    public int Executed { get; init; }

    // 48h outcome counts (see DecisionOutcomes).
    public int Resolved { get; init; }
    public int StockedOut { get; init; }

    /// <summary>
    /// Decisions a manager dismissed that nevertheless ended in a stockout —
    /// the strongest signal that the rule's alerts were warranted.
    /// </summary>
    public int DismissedThenStockedOut { get; init; }
}
