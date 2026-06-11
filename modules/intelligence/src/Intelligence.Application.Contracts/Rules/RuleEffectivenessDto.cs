using System;

namespace Intelligence.Rules;

/// <summary>
/// Per-rule effectiveness aggregate surfaced on the Inventory Rules page:
/// workflow status counts plus 48h outcome counts recorded by the decision
/// outcome scanner. Mirrors the RuleEffectivenessRow read model.
/// </summary>
public class RuleEffectivenessDto
{
    public Guid RuleId { get; set; }

    public int TotalDecisions { get; set; }

    // Status workflow counts.
    public int Pending { get; set; }
    public int Acknowledged { get; set; }
    public int Dismissed { get; set; }
    public int Executed { get; set; }

    // 48h outcome counts (see DecisionOutcomes).
    public int Resolved { get; set; }
    public int StockedOut { get; set; }

    /// <summary>Decisions dismissed by a manager that nevertheless ended in a stockout.</summary>
    public int DismissedThenStockedOut { get; set; }
}
