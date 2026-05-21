using System;
using Intelligence.Events;
using Volo.Abp.Domain.Entities.Auditing;

namespace Intelligence.Entities;

public class AppDecisionLog : CreationAuditedAggregateRoot<Guid>
{
    public Guid RuleId { get; }
    public Guid ProductId { get; }
    public Guid? BranchId { get; }
    public Guid? SourceBranchId { get; }
    public Guid? TargetBranchId { get; }
    public string DecisionType { get; } = null!;
    public string Reasoning { get; } = null!;
    public string? SuggestedAction { get; }
    public int? StockAtEvaluation { get; }
    public int? DaysWithoutSale { get; }

    public string Status { get; private set; } = "Pending";
    public DateTime? AcknowledgedAt { get; private set; }
    public Guid? AcknowledgedByUserId { get; private set; }

    protected AppDecisionLog() { }

    public AppDecisionLog(
        Guid id,
        Guid ruleId,
        Guid productId,
        Guid? branchId,
        string decisionType,
        string reasoning,
        string? suggestedAction = null,
        int? stockAtEvaluation = null,
        int? daysWithoutSale = null,
        Guid? sourceBranchId = null,
        Guid? targetBranchId = null)
        : base(id)
    {
        RuleId = ruleId;
        ProductId = productId;
        BranchId = branchId;
        DecisionType = decisionType;
        Reasoning = reasoning;
        SuggestedAction = suggestedAction;
        StockAtEvaluation = stockAtEvaluation;
        DaysWithoutSale = daysWithoutSale;
        SourceBranchId = sourceBranchId;
        TargetBranchId = targetBranchId;
        Status = "Pending";

        AddDistributedEvent(new DecisionMadeEto
        {
            DecisionLogId = Id,
            DecisionType = decisionType,
            ProductId = productId,
            BranchId = branchId
        });
    }

    public void Acknowledge(Guid userId)
    {
        Status = "Acknowledged";
        AcknowledgedAt = DateTime.UtcNow;
        AcknowledgedByUserId = userId;
    }
}
