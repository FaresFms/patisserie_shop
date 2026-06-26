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

    /// <summary>
    /// A MANUAL low-stock report raised by a cashier from the POS — not a rule-engine
    /// decision. It carries the sentinel rule id (<see cref="Intelligence.IntelligenceConstants.CashierReportRuleId"/>)
    /// because AppDecisionLog.RuleId is non-null. Informational: the manager acknowledges
    /// or dismisses it; there is no corrective document to execute.
    /// </summary>
    public const string StockReport = "StockReport";

    /// <summary>
    /// Raised when a cashier closes a shift and counted cash does not match expected cash.
    /// Informational: the branch manager/admin acknowledges or dismisses it.
    /// </summary>
    public const string CashierVariance = "CashierVariance";

    public const string IngredientShortage = "IngredientShortage";
    public const string ProductionShortageRisk = "ProductionShortageRisk";
    public const string HighKitchenWaste = "HighKitchenWaste";
    public const string ProductionCostVariance = "ProductionCostVariance";
    public const string LateProductionRisk = "LateProductionRisk";
    public const string UnfulfilledBranchRequest = "UnfulfilledBranchRequest";

    public static readonly string[] All =
    {
        LowStockAlert,
        ExcessStockAlert,
        DeadStockFlag,
        TransferSuggestion,
        ReorderSuggestion,
        StockoutRisk,
        ExpiryAlert,
        WasteWriteOff,
        StockReport,
        CashierVariance,
        IngredientShortage,
        ProductionShortageRisk,
        HighKitchenWaste,
        ProductionCostVariance,
        LateProductionRisk,
        UnfulfilledBranchRequest
    };

    public static bool IsValid(string? decisionType)
        => !string.IsNullOrWhiteSpace(decisionType) && Array.IndexOf(All, decisionType) >= 0;
}
