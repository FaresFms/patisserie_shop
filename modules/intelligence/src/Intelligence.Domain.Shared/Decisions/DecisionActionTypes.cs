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

    public static readonly string[] All =
    {
        PurchaseOrder,
        StockTransfer
    };

    public static bool IsValid(string? actionType)
        => !string.IsNullOrWhiteSpace(actionType) && Array.IndexOf(All, actionType) >= 0;
}
