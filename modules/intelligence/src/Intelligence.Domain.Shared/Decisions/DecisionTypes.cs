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

    /// <summary>Raised by DaysOfCover rules: stock will run out within the threshold days.</summary>
    public const string StockoutRisk = "StockoutRisk";

    /// <summary>Raised by ExpiringSoon rules: a stock batch expires within the threshold days.</summary>
    public const string ExpiryAlert = "ExpiryAlert";

    /// <summary>
    /// Raised by ExpiredStock rules: live batches are already PAST their expiry date —
    /// the suggested action is a stock write-off. Destructive, so it NEVER runs on
    /// autopilot; a human executes it from the decision log.
    /// </summary>
    public const string WasteWriteOff = "WasteWriteOff";

    public static readonly string[] All =
    {
        LowStockAlert,
        ExcessStockAlert,
        DeadStockFlag,
        TransferSuggestion,
        ReorderSuggestion,
        StockoutRisk,
        ExpiryAlert,
        WasteWriteOff
    };

    public static bool IsValid(string? decisionType)
        => !string.IsNullOrWhiteSpace(decisionType) && Array.IndexOf(All, decisionType) >= 0;
}
