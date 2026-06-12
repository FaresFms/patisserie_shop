using System;

namespace Intelligence.Rules;

/// <summary>
/// "Autopilot" level for an inventory rule — what happens when the rule fires.
/// SuggestOnly leaves the decision Pending for a human; CreateDraft immediately
/// creates the corrective draft document and marks the decision Executed;
/// AutoSubmit additionally submits the created document for approval. Humans
/// still approve documents in every mode (configurable automation with
/// human-in-the-loop). Stored as a flat string on AppInventoryRule.ActionMode.
/// </summary>
public static class RuleActionModes
{
    public const string SuggestOnly = "SuggestOnly";
    public const string CreateDraft = "CreateDraft";
    public const string AutoSubmit = "AutoSubmit";

    public static readonly string[] All =
    {
        SuggestOnly,
        CreateDraft,
        AutoSubmit
    };

    public static bool IsValid(string? actionMode)
        => !string.IsNullOrWhiteSpace(actionMode) && Array.IndexOf(All, actionMode) >= 0;
}
