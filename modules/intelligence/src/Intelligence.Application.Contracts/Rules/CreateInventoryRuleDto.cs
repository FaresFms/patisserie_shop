using System;
using System.ComponentModel.DataAnnotations;

namespace Intelligence.Rules;

public class CreateInventoryRuleDto
{
    [Required]
    [StringLength(128)]
    public string RuleName { get; set; } = null!;

    [Required]
    [StringLength(32)]
    public string RuleType { get; set; } = null!;

    public Guid? ProductId { get; set; }

    public Guid? BranchId { get; set; }

    public int? ThresholdValue { get; set; }

    public int? ThresholdDays { get; set; }

    [StringLength(256)]
    public string? SuggestedAction { get; set; }

    public int Priority { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Autopilot level applied when the rule fires (see RuleActionModes).</summary>
    [Required]
    [StringLength(32)]
    public string ActionMode { get; set; } = RuleActionModes.SuggestOnly;
}
