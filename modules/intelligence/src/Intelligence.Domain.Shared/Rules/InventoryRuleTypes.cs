using System;

namespace Intelligence.Rules;

/// <summary>
/// The supported inventory rule types evaluated by the DecisionMakerService
/// (real-time) and the background jobs (DeadStock / TransferSuggestion).
/// Stored as a flat string on AppInventoryRule.RuleType.
/// </summary>
public static class InventoryRuleTypes
{
    public const string LowStock = "LowStock";
    public const string ExcessStock = "ExcessStock";
    public const string DeadStock = "DeadStock";
    public const string TransferSuggestion = "TransferSuggestion";

    /// <summary>
    /// Time-aware low-stock rule: fires when QuantityOnHand ÷ AvgDailySales30 falls
    /// below ThresholdValue (interpreted as a number of days of cover).
    /// </summary>
    public const string DaysOfCover = "DaysOfCover";

    public static readonly string[] All =
    {
        LowStock,
        ExcessStock,
        DeadStock,
        TransferSuggestion,
        DaysOfCover
    };

    public static bool IsValid(string? ruleType)
        => !string.IsNullOrWhiteSpace(ruleType) && Array.IndexOf(All, ruleType) >= 0;

    /// <summary>
    /// DeadStock is the only type measured in days (ThresholdDays);
    /// every other type uses ThresholdValue. Note: DaysOfCover also reuses
    /// ThresholdValue — the value is interpreted as days of cover, not a quantity.
    /// </summary>
    public static bool UsesThresholdDays(string ruleType)
        => ruleType == DeadStock;
}
