using System;
using Volo.Abp.Application.Dtos;

namespace Intelligence.Rules;

public class InventoryRuleDto : EntityDto<Guid>
{
    public string RuleName { get; set; } = null!;
    public string RuleType { get; set; } = null!;
    public Guid? ProductId { get; set; }
    public Guid? BranchId { get; set; }
    public int? ThresholdValue { get; set; }
    public int? ThresholdDays { get; set; }
    public string? SuggestedAction { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Autopilot level applied when the rule fires (see RuleActionModes).</summary>
    public string ActionMode { get; set; } = RuleActionModes.SuggestOnly;

    public DateTime CreationTime { get; set; }

    /// <summary>Resolved by the app service for the Scope column. Null when the rule is product-global.</summary>
    public string? ProductName { get; set; }

    /// <summary>Resolved by the app service for the Scope column. Null when the rule is branch-global.</summary>
    public string? BranchName { get; set; }
}
