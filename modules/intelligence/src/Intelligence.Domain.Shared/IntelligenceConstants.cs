using System;

namespace Intelligence;

/// <summary>
/// Well-known fixed identifiers shared across the Intelligence module.
/// </summary>
public static class IntelligenceConstants
{
    /// <summary>
    /// Sentinel <c>AppInventoryRule.Id</c> for manual cashier-raised low-stock reports.
    /// AppDecisionLog.RuleId is non-null, but a manual report has no real rule behind it,
    /// so it points at this single seeded, INACTIVE "Cashier Stock Report" rule. The rule
    /// is inactive on purpose — the engine never evaluates it — it only exists to satisfy
    /// the non-null RuleId and to give the decision log a readable rule name.
    /// </summary>
    public static readonly Guid CashierReportRuleId =
        Guid.Parse("c45f1e00-0000-4000-a000-000000000001");

    /// <summary>
    /// Sentinel rule id for manual cashier cash-drawer variance notifications. It is not
    /// evaluated by the rules engine; it lets the decision log carry operational alerts.
    /// </summary>
    public static readonly Guid CashierVarianceRuleId =
        Guid.Parse("c45f1e00-0000-4000-a000-000000000002");

    /// <summary>
    /// Sentinel rule id for deterministic production-module alerts. These alerts are
    /// produced from production orders, branch demand, and waste telemetry, not from
    /// AppInventoryRule threshold evaluation.
    /// </summary>
    public static readonly Guid ProductionOperationsRuleId =
        Guid.Parse("c45f1e00-0000-4000-a000-000000000003");
}
