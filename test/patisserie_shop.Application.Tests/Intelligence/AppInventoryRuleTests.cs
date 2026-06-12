using System;
using Intelligence;
using Intelligence.Entities;
using Intelligence.Rules;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace patisserie_shop.Intelligence;

/// <summary>
/// Pure unit tests for the AppInventoryRule invariants: rule-type validation, the
/// "exactly the right threshold field" rule, and ActionMode (autopilot) validation.
/// Uses the internal constructor via InternalsVisibleTo (normally only
/// InventoryRuleManager constructs the aggregate).
/// </summary>
public class AppInventoryRuleTests
{
    private static AppInventoryRule NewRule(
        string ruleType = InventoryRuleTypes.LowStock,
        int? thresholdValue = 5,
        int? thresholdDays = null,
        string? actionMode = null)
        => new(
            Guid.NewGuid(),
            ruleName: "Test Rule",
            ruleType: ruleType,
            thresholdValue: thresholdValue,
            thresholdDays: thresholdDays,
            actionMode: actionMode);

    [Fact]
    public void Day_Based_Rules_Require_ThresholdDays_And_Null_Out_ThresholdValue()
    {
        // DeadStock and ExpiringSoon are the two day-measured rule types.
        foreach (var ruleType in new[] { InventoryRuleTypes.DeadStock, InventoryRuleTypes.ExpiringSoon })
        {
            Should.Throw<BusinessException>(() =>
                    NewRule(ruleType, thresholdValue: 5, thresholdDays: null))
                .Code.ShouldBe(IntelligenceErrorCodes.ThresholdDaysRequired);

            // Even when a quantity is also supplied, it is forced to null.
            var rule = NewRule(ruleType, thresholdValue: 5, thresholdDays: 30);
            rule.ThresholdDays.ShouldBe(30);
            rule.ThresholdValue.ShouldBeNull();
        }
    }

    [Fact]
    public void Quantity_Rules_Require_ThresholdValue_And_Null_Out_ThresholdDays()
    {
        foreach (var ruleType in new[]
                 {
                     InventoryRuleTypes.LowStock,
                     InventoryRuleTypes.ExcessStock,
                     InventoryRuleTypes.TransferSuggestion,
                     InventoryRuleTypes.DaysOfCover // days-of-cover count rides in ThresholdValue
                 })
        {
            Should.Throw<BusinessException>(() =>
                    NewRule(ruleType, thresholdValue: null, thresholdDays: 30))
                .Code.ShouldBe(IntelligenceErrorCodes.ThresholdValueRequired);

            var rule = NewRule(ruleType, thresholdValue: 10, thresholdDays: 30);
            rule.ThresholdValue.ShouldBe(10);
            rule.ThresholdDays.ShouldBeNull();
        }
    }

    [Fact]
    public void Invalid_RuleType_Is_Rejected()
    {
        Should.Throw<BusinessException>(() => NewRule(ruleType: "MachineLearning"))
            .Code.ShouldBe(IntelligenceErrorCodes.InvalidRuleType);
    }

    [Fact]
    public void SetActionMode_Accepts_Only_Known_Autopilot_Levels_And_Defaults_To_SuggestOnly()
    {
        var rule = NewRule();
        rule.ActionMode.ShouldBe(RuleActionModes.SuggestOnly); // default

        rule.SetActionMode(RuleActionModes.CreateDraft);
        rule.ActionMode.ShouldBe(RuleActionModes.CreateDraft);
        rule.SetActionMode(RuleActionModes.AutoSubmit);
        rule.ActionMode.ShouldBe(RuleActionModes.AutoSubmit);

        Should.Throw<BusinessException>(() => rule.SetActionMode("FullyAutonomous"))
            .Code.ShouldBe(IntelligenceErrorCodes.InvalidRuleActionMode);
        Should.Throw<BusinessException>(() => rule.SetActionMode(null!))
            .Code.ShouldBe(IntelligenceErrorCodes.InvalidRuleActionMode);
        rule.ActionMode.ShouldBe(RuleActionModes.AutoSubmit); // unchanged after the failed sets
    }

    [Fact]
    public void UpdateInfo_Reenforces_The_Threshold_Invariant()
    {
        var rule = NewRule(InventoryRuleTypes.LowStock, thresholdValue: 5);

        // Retyping the rule to DeadStock without days must fail...
        Should.Throw<BusinessException>(() => rule.UpdateInfo(
                "Renamed", InventoryRuleTypes.DeadStock,
                productId: null, branchId: null,
                thresholdValue: 5, thresholdDays: null,
                suggestedAction: null, priority: 1, isActive: true))
            .Code.ShouldBe(IntelligenceErrorCodes.ThresholdDaysRequired);

        // ...and a valid retype swaps the threshold fields.
        rule.UpdateInfo(
            "Renamed", InventoryRuleTypes.DeadStock,
            productId: null, branchId: null,
            thresholdValue: null, thresholdDays: 14,
            suggestedAction: "Discount it", priority: 1, isActive: false);

        rule.RuleType.ShouldBe(InventoryRuleTypes.DeadStock);
        rule.ThresholdDays.ShouldBe(14);
        rule.ThresholdValue.ShouldBeNull();
        rule.IsActive.ShouldBeFalse();
    }
}
