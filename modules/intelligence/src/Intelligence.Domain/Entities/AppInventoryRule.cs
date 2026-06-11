using System;
using Intelligence.Rules;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Intelligence.Entities;

public class AppInventoryRule : FullAuditedAggregateRoot<Guid>
{
    public string RuleName { get; private set; } = null!;
    public string RuleType { get; private set; } = null!;
    public Guid? ProductId { get; private set; }
    public Guid? BranchId { get; private set; }
    public int? ThresholdValue { get; private set; }
    public int? ThresholdDays { get; private set; }
    public string? SuggestedAction { get; private set; }
    public int Priority { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Autopilot level applied when this rule fires (see <see cref="RuleActionModes"/>).
    /// SuggestOnly = leave the decision Pending for a human (default);
    /// CreateDraft = auto-create the corrective draft document and mark Executed;
    /// AutoSubmit = same, plus submit the created document for approval.
    /// </summary>
    public string ActionMode { get; private set; } = RuleActionModes.SuggestOnly;

    protected AppInventoryRule() { }

    internal AppInventoryRule(
        Guid id,
        string ruleName,
        string ruleType,
        Guid? productId = null,
        Guid? branchId = null,
        int? thresholdValue = null,
        int? thresholdDays = null,
        string? suggestedAction = null,
        int priority = 0,
        bool isActive = true,
        string? actionMode = null)
        : base(id)
    {
        SetRuleName(ruleName);
        SetTypeAndThresholds(ruleType, thresholdValue, thresholdDays);
        SetScope(productId, branchId);
        SetSuggestedAction(suggestedAction);
        SetActionMode(actionMode ?? RuleActionModes.SuggestOnly);
        Priority = priority;
        IsActive = isActive;
    }

    /// <summary>Mutates every field the management UI can edit, re-enforcing the threshold invariant.</summary>
    public void UpdateInfo(
        string ruleName,
        string ruleType,
        Guid? productId,
        Guid? branchId,
        int? thresholdValue,
        int? thresholdDays,
        string? suggestedAction,
        int priority,
        bool isActive,
        string? actionMode = null)
    {
        SetRuleName(ruleName);
        SetTypeAndThresholds(ruleType, thresholdValue, thresholdDays);
        SetScope(productId, branchId);
        SetSuggestedAction(suggestedAction);
        SetActionMode(actionMode ?? RuleActionModes.SuggestOnly);
        Priority = priority;
        IsActive = isActive;
    }

    public void SetRuleName(string ruleName)
    {
        RuleName = Check.NotNullOrWhiteSpace(ruleName, nameof(ruleName), maxLength: 128).Trim();
    }

    public void SetScope(Guid? productId, Guid? branchId)
    {
        ProductId = productId;
        BranchId = branchId;
    }

    public void SetSuggestedAction(string? suggestedAction)
    {
        SuggestedAction = string.IsNullOrWhiteSpace(suggestedAction)
            ? null
            : Check.Length(suggestedAction.Trim(), nameof(suggestedAction), maxLength: 256);
    }

    public void SetActionMode(string actionMode)
    {
        if (!RuleActionModes.IsValid(actionMode))
        {
            throw new BusinessException(IntelligenceErrorCodes.InvalidRuleActionMode)
                .WithData("ActionMode", actionMode ?? "(null)");
        }

        ActionMode = actionMode;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Validates the rule type and forces exactly the correct threshold field to be set:
    /// DeadStock uses ThresholdDays (ThresholdValue nulled); every other type uses
    /// ThresholdValue (ThresholdDays nulled).
    /// </summary>
    private void SetTypeAndThresholds(string ruleType, int? thresholdValue, int? thresholdDays)
    {
        if (!InventoryRuleTypes.IsValid(ruleType))
        {
            throw new BusinessException(IntelligenceErrorCodes.InvalidRuleType)
                .WithData("RuleType", ruleType ?? "(null)");
        }

        RuleType = ruleType;

        if (InventoryRuleTypes.UsesThresholdDays(ruleType))
        {
            if (!thresholdDays.HasValue)
            {
                throw new BusinessException(IntelligenceErrorCodes.ThresholdDaysRequired);
            }

            ThresholdDays = thresholdDays;
            ThresholdValue = null;
        }
        else
        {
            if (!thresholdValue.HasValue)
            {
                throw new BusinessException(IntelligenceErrorCodes.ThresholdValueRequired);
            }

            ThresholdValue = thresholdValue;
            ThresholdDays = null;
        }
    }
}
