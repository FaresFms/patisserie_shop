using System;
using Intelligence.Decisions;
using Intelligence.Events;
using Volo.Abp;
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

    public string Status { get; private set; } = DecisionLogStatuses.Pending;
    public DateTime? AcknowledgedAt { get; private set; }
    public Guid? AcknowledgedByUserId { get; private set; }

    /// <summary>
    /// Kind of corrective document created when this decision was executed
    /// (see <see cref="DecisionActionTypes"/>). Set exclusively during the single
    /// allowed Pending→Executed transition; null when execution created nothing.
    /// </summary>
    public string? ExecutedActionType { get; private set; }

    /// <summary>Id of the document referenced by <see cref="ExecutedActionType"/>.</summary>
    public Guid? ExecutedActionId { get; private set; }

    /// <summary>
    /// Whether the underlying problem actually got resolved, recorded ~48h after creation
    /// by the DecisionOutcomeScanner (see <see cref="DecisionOutcomes"/>). Null until evaluated.
    /// </summary>
    public string? Outcome { get; private set; }

    /// <summary>UTC moment the <see cref="Outcome"/> was recorded.</summary>
    public DateTime? OutcomeEvaluatedAt { get; private set; }

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
        Status = DecisionLogStatuses.Pending;

        AddDistributedEvent(new DecisionMadeEto
        {
            DecisionLogId = Id,
            DecisionType = decisionType,
            ProductId = productId,
            BranchId = branchId
        });
    }

    public void Acknowledge(Guid userId) => TransitionTo(DecisionLogStatuses.Acknowledged, userId);

    public void Dismiss(Guid userId) => TransitionTo(DecisionLogStatuses.Dismissed, userId);

    public void MarkExecuted(Guid userId, string? actionType = null, Guid? actionId = null)
    {
        if (actionType != null && !DecisionActionTypes.IsValid(actionType))
        {
            throw new BusinessException(IntelligenceErrorCodes.InvalidDecisionActionType)
                .WithData("ActionType", actionType);
        }

        ExecutedActionType = actionType;
        ExecutedActionId = actionId;
        TransitionTo(DecisionLogStatuses.Executed, userId);
    }

    /// <summary>
    /// Records whether the problem behind this decision actually got resolved.
    /// This is the SECOND AND LAST permitted mutation of the ledger row (the first
    /// being the single Pending→Acknowledged/Dismissed/Executed status transition):
    /// Outcome is write-once — any attempt to record it twice throws, keeping the
    /// ledger immutable once evaluated.
    /// </summary>
    public void RecordOutcome(string outcome, DateTime evaluatedAtUtc)
    {
        if (Outcome != null)
        {
            throw new BusinessException(IntelligenceErrorCodes.DecisionOutcomeAlreadyRecorded)
                .WithData("ExistingOutcome", Outcome)
                .WithData("AttemptedOutcome", outcome);
        }

        if (!DecisionOutcomes.IsValid(outcome))
        {
            throw new BusinessException(IntelligenceErrorCodes.InvalidDecisionOutcome)
                .WithData("Outcome", outcome ?? "(null)");
        }

        Outcome = outcome;
        OutcomeEvaluatedAt = evaluatedAtUtc;
    }

    /// <summary>
    /// Single point of mutation for the workflow status. A decision log is born
    /// Pending and may only move once — to Acknowledged, Dismissed, or Executed.
    /// Any attempt to transition a non-Pending log throws so historical entries
    /// stay immutable.
    /// </summary>
    private void TransitionTo(string newStatus, Guid userId)
    {
        if (Status != DecisionLogStatuses.Pending)
        {
            throw new BusinessException(IntelligenceErrorCodes.DecisionLogNotPending)
                .WithData("CurrentStatus", Status)
                .WithData("AttemptedStatus", newStatus);
        }

        Status = newStatus;
        AcknowledgedAt = DateTime.UtcNow;
        AcknowledgedByUserId = userId;
    }
}
