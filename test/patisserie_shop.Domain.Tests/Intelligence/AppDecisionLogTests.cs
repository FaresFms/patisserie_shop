using System;
using System.Linq;
using Intelligence;
using Intelligence.Decisions;
using Intelligence.Entities;
using Intelligence.Events;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace patisserie_shop.Intelligence;

/// <summary>
/// Pure unit tests for the AppDecisionLog ledger row: born Pending, exactly one
/// status transition allowed, write-once Outcome — the controlled-mutation rules
/// that keep the decision ledger immutable.
/// </summary>
public class AppDecisionLogTests
{
    private static AppDecisionLog NewLog() => new(
        Guid.NewGuid(),
        ruleId: Guid.NewGuid(),
        productId: Guid.NewGuid(),
        branchId: Guid.NewGuid(),
        decisionType: DecisionTypes.LowStockAlert,
        reasoning: "Stock=3 is below threshold=5",
        stockAtEvaluation: 3);

    [Fact]
    public void New_Log_Is_Born_Pending_And_Publishes_DecisionMadeEto()
    {
        var log = NewLog();

        log.Status.ShouldBe(DecisionLogStatuses.Pending);
        log.Outcome.ShouldBeNull();
        log.AcknowledgedAt.ShouldBeNull();

        var eto = log.GetDistributedEvents()
            .Select(e => e.EventData)
            .OfType<DecisionMadeEto>()
            .ShouldHaveSingleItem();
        eto.DecisionLogId.ShouldBe(log.Id);
        eto.DecisionType.ShouldBe(DecisionTypes.LowStockAlert);
        eto.ProductId.ShouldBe(log.ProductId);
    }

    [Fact]
    public void Acknowledge_And_Dismiss_Transition_Once_And_Stamp_The_User()
    {
        var userId = Guid.NewGuid();

        var acknowledged = NewLog();
        acknowledged.Acknowledge(userId);
        acknowledged.Status.ShouldBe(DecisionLogStatuses.Acknowledged);
        acknowledged.AcknowledgedByUserId.ShouldBe(userId);
        acknowledged.AcknowledgedAt.ShouldNotBeNull();

        var dismissed = NewLog();
        dismissed.Dismiss(userId);
        dismissed.Status.ShouldBe(DecisionLogStatuses.Dismissed);
        dismissed.AcknowledgedByUserId.ShouldBe(userId);
    }

    [Fact]
    public void MarkExecuted_Records_The_Created_Document_Link()
    {
        var log = NewLog();
        var poId = Guid.NewGuid();

        log.MarkExecuted(Guid.NewGuid(), DecisionActionTypes.PurchaseOrder, poId);

        log.Status.ShouldBe(DecisionLogStatuses.Executed);
        log.ExecutedActionType.ShouldBe(DecisionActionTypes.PurchaseOrder);
        log.ExecutedActionId.ShouldBe(poId);
    }

    [Fact]
    public void MarkExecuted_Rejects_Invalid_Action_Type_And_Stays_Pending()
    {
        var log = NewLog();

        var ex = Should.Throw<BusinessException>(() =>
            log.MarkExecuted(Guid.NewGuid(), "Email", Guid.NewGuid()));

        ex.Code.ShouldBe(IntelligenceErrorCodes.InvalidDecisionActionType);
        log.Status.ShouldBe(DecisionLogStatuses.Pending);
        log.ExecutedActionId.ShouldBeNull();
    }

    [Fact]
    public void Second_Status_Transition_Throws_DecisionLogNotPending()
    {
        var userId = Guid.NewGuid();

        var acknowledged = NewLog();
        acknowledged.Acknowledge(userId);
        Should.Throw<BusinessException>(() => acknowledged.Dismiss(userId))
            .Code.ShouldBe(IntelligenceErrorCodes.DecisionLogNotPending);

        var dismissed = NewLog();
        dismissed.Dismiss(userId);
        Should.Throw<BusinessException>(() => dismissed.MarkExecuted(userId))
            .Code.ShouldBe(IntelligenceErrorCodes.DecisionLogNotPending);

        var executed = NewLog();
        executed.MarkExecuted(userId, DecisionActionTypes.StockTransfer, Guid.NewGuid());
        Should.Throw<BusinessException>(() => executed.Acknowledge(userId))
            .Code.ShouldBe(IntelligenceErrorCodes.DecisionLogNotPending);
    }

    [Fact]
    public void RecordOutcome_Sets_Outcome_And_Timestamp()
    {
        var log = NewLog();
        var evaluatedAt = new DateTime(2026, 6, 3, 2, 0, 0, DateTimeKind.Utc);

        log.RecordOutcome(DecisionOutcomes.Resolved, evaluatedAt);

        log.Outcome.ShouldBe(DecisionOutcomes.Resolved);
        log.OutcomeEvaluatedAt.ShouldBe(evaluatedAt);
        // Outcome evaluation never touches the workflow status.
        log.Status.ShouldBe(DecisionLogStatuses.Pending);
    }

    [Fact]
    public void RecordOutcome_Rejects_Invalid_Values()
    {
        var log = NewLog();

        Should.Throw<BusinessException>(() => log.RecordOutcome("Fixed", DateTime.UtcNow))
            .Code.ShouldBe(IntelligenceErrorCodes.InvalidDecisionOutcome);
        log.Outcome.ShouldBeNull();
    }

    [Fact]
    public void RecordOutcome_Is_Write_Once()
    {
        var log = NewLog();
        log.RecordOutcome(DecisionOutcomes.Unresolved, DateTime.UtcNow);

        var ex = Should.Throw<BusinessException>(() =>
            log.RecordOutcome(DecisionOutcomes.Resolved, DateTime.UtcNow));

        ex.Code.ShouldBe(IntelligenceErrorCodes.DecisionOutcomeAlreadyRecorded);
        log.Outcome.ShouldBe(DecisionOutcomes.Unresolved);
    }
}
