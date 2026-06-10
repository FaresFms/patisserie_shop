using System;
using Volo.Abp.Application.Dtos;

namespace Intelligence.Decisions;

public class DecisionLogDto : EntityDto<Guid>
{
    public Guid RuleId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? SourceBranchId { get; set; }
    public Guid? TargetBranchId { get; set; }

    public string DecisionType { get; set; } = null!;
    public string Reasoning { get; set; } = null!;
    public string? SuggestedAction { get; set; }
    public int? StockAtEvaluation { get; set; }
    public int? DaysWithoutSale { get; set; }

    public string Status { get; set; } = null!;
    public DateTime? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }

    /// <summary>Corrective document kind created on execution (see DecisionActionTypes).</summary>
    public string? ExecutedActionType { get; set; }

    /// <summary>Id of the corrective document created on execution.</summary>
    public Guid? ExecutedActionId { get; set; }

    public DateTime CreationTime { get; set; }

    /// <summary>Resolved by the app service from the originating AppInventoryRule.</summary>
    public string? RuleName { get; set; }

    /// <summary>Resolved by the app service from the product lookup.</summary>
    public string? ProductName { get; set; }

    /// <summary>Resolved by the app service from the branch lookup.</summary>
    public string? BranchName { get; set; }

    public string? SourceBranchName { get; set; }
    public string? TargetBranchName { get; set; }

    /// <summary>
    /// Populated client-side from IIdentityUserAppService to keep this contract
    /// free of an Identity dependency (mirrors how Branches.razor resolves managers).
    /// </summary>
    public string? AcknowledgedByUserName { get; set; }
}
