using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace Intelligence.Entities;

public class AppInventoryRule : FullAuditedAggregateRoot<Guid>
{
    public string RuleName { get; set; } = null!;
    public string RuleType { get; set; } = null!;
    public Guid? ProductId { get; set; }
    public Guid? BranchId { get; set; }
    public int? ThresholdValue { get; set; }
    public int? ThresholdDays { get; set; }
    public string? SuggestedAction { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;

    protected AppInventoryRule() { }

    public AppInventoryRule(
        Guid id,
        string ruleName,
        string ruleType,
        Guid? productId = null,
        Guid? branchId = null,
        int? thresholdValue = null,
        int? thresholdDays = null,
        string? suggestedAction = null,
        int priority = 0,
        bool isActive = true)
        : base(id)
    {
        RuleName = ruleName;
        RuleType = ruleType;
        ProductId = productId;
        BranchId = branchId;
        ThresholdValue = thresholdValue;
        ThresholdDays = thresholdDays;
        SuggestedAction = suggestedAction;
        Priority = priority;
        IsActive = isActive;
    }
}
