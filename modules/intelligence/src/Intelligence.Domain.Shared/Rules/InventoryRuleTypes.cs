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

    public static readonly string[] All =
    {
        LowStock,
        ExcessStock,
        DeadStock,
        TransferSuggestion
    };

    public static bool IsValid(string? ruleType)
        => !string.IsNullOrWhiteSpace(ruleType) && Array.IndexOf(All, ruleType) >= 0;

    /// <summary>
    /// DeadStock is the only type measured in days (ThresholdDays);
    /// every other type uses a stock quantity (ThresholdValue).
    /// </summary>
    public static bool UsesThresholdDays(string ruleType)
        => ruleType == DeadStock;
}
