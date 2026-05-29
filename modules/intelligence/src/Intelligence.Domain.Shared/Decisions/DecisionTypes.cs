using System;

namespace Intelligence.Decisions;

/// <summary>
/// DecisionType values written by DecisionMakerService (real-time) and the
/// background jobs (DeadStock / TransferSuggestion). Stored flat as
/// AppDecisionLog.DecisionType.
/// </summary>
public static class DecisionTypes
{
    public const string LowStockAlert = "LowStockAlert";
    public const string ExcessStockAlert = "ExcessStockAlert";
    public const string DeadStockFlag = "DeadStockFlag";
    public const string TransferSuggestion = "TransferSuggestion";
    public const string ReorderSuggestion = "ReorderSuggestion";

    public static readonly string[] All =
    {
        LowStockAlert,
        ExcessStockAlert,
        DeadStockFlag,
        TransferSuggestion,
        ReorderSuggestion
    };

    public static bool IsValid(string? decisionType)
        => !string.IsNullOrWhiteSpace(decisionType) && Array.IndexOf(All, decisionType) >= 0;
}
