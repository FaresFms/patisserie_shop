using System;

namespace patisserie_shop.Decisions;

/// <summary>
/// Input for consolidating pending reorder-type decisions (LowStockAlert,
/// ReorderSuggestion, StockoutRisk) into draft purchase orders grouped by
/// (supplier × destination branch).
/// </summary>
public class ConsolidateReordersInput
{
    /// <summary>
    /// Optional branch scope. When set, only pending reorder decisions for this
    /// branch are consolidated (mirrors the Decision Log branch filter).
    /// </summary>
    public Guid? BranchId { get; set; }

    /// <summary>
    /// Optional explicit set of decision log ids to consolidate. When provided, the
    /// candidate set is restricted to these ids (still filtered to pending reorder
    /// types). When null/empty, all matching pending reorder decisions are taken.
    /// </summary>
    public Guid[]? DecisionLogIds { get; set; }
}
