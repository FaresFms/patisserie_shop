using System;

namespace Intelligence.Decisions;

/// <summary>
/// Kinds of corrective documents that can be created when a decision log entry is
/// executed. Stored flat as AppDecisionLog.ExecutedActionType alongside the
/// document's Id (ExecutedActionId) so the log links back to what it produced.
/// </summary>
public static class DecisionActionTypes
{
    public const string PurchaseOrder = "PurchaseOrder";
    public const string StockTransfer = "StockTransfer";
    public const string BranchProductionRequest = "BranchProductionRequest";

    /// <summary>
    /// The corrective action was a direct stock adjustment (e.g. a waste write-off
    /// recorded through BranchInventory.AdjustStock) rather than a draft document.
    /// ExecutedActionId stays null — the audit trail is the AppStockMovement ledger.
    /// </summary>
    public const string StockAdjustment = "StockAdjustment";

    public static readonly string[] All =
    {
        PurchaseOrder,
        StockTransfer,
        BranchProductionRequest,
        StockAdjustment
    };

    public static bool IsValid(string? actionType)
        => !string.IsNullOrWhiteSpace(actionType) && Array.IndexOf(All, actionType) >= 0;
}
