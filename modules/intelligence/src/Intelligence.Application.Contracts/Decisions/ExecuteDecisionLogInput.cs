using System;

namespace Intelligence.Decisions;

/// <summary>
/// Optional payload for ExecuteAsync: records which corrective document (if any)
/// was created as a result of executing the decision. Both fields are optional —
/// executing without a document is still allowed (e.g. ExcessStock / DeadStock).
/// </summary>
public class ExecuteDecisionLogInput
{
    /// <summary>One of the DecisionActionTypes constants, or null.</summary>
    public string? ActionType { get; set; }

    /// <summary>Id of the created document, or null.</summary>
    public Guid? ActionId { get; set; }
}
