using System;

namespace patisserie_shop.Decisions;

/// <summary>
/// Outcome of executing a decision log entry: whether a corrective document was
/// created and, if so, what it is — so the UI can announce and link to it.
/// </summary>
public class DecisionActionResultDto
{
    /// <summary>True when a draft document was created as part of execution.</summary>
    public bool ActionCreated { get; set; }

    /// <summary>One of the Intelligence DecisionActionTypes constants, or null.</summary>
    public string? ActionType { get; set; }

    /// <summary>Id of the created document, or null.</summary>
    public Guid? ActionId { get; set; }

    /// <summary>Human-friendly reference (PO number / transfer reference) for the snackbar.</summary>
    public string? ActionNumber { get; set; }
}
