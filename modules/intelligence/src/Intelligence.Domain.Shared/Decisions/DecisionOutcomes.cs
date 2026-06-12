using System;

namespace Intelligence.Decisions;

/// <summary>
/// Outcome values recorded on AppDecisionLog ~48h after the decision was created,
/// by the DecisionOutcomeScanner: did the underlying problem actually get resolved?
/// Stored as a flat string on AppDecisionLog.Outcome (null = not yet evaluated).
/// </summary>
public static class DecisionOutcomes
{
    /// <summary>The condition that triggered the decision is no longer present.</summary>
    public const string Resolved = "Resolved";

    /// <summary>The condition that triggered the decision still persists.</summary>
    public const string Unresolved = "Unresolved";

    /// <summary>The product fully stocked out at the evaluated branch — the worst case.</summary>
    public const string StockedOut = "StockedOut";

    public static readonly string[] All =
    {
        Resolved,
        Unresolved,
        StockedOut
    };

    public static bool IsValid(string? outcome)
        => !string.IsNullOrWhiteSpace(outcome) && Array.IndexOf(All, outcome) >= 0;
}
