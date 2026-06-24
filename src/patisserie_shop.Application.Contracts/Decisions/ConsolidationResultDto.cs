using System;
using System.Collections.Generic;

namespace patisserie_shop.Decisions;

/// <summary>
/// Outcome of a reorder consolidation run: how many pending decisions were turned
/// into how many draft purchase orders, how many were skipped (product without a
/// default supplier), and a per-PO breakdown for the UI to announce/link.
/// </summary>
public class ConsolidationResultDto
{
    /// <summary>Total pending reorder decisions linked to a created draft PO.</summary>
    public int DecisionsConsolidated { get; set; }

    /// <summary>Number of draft purchase orders created (one per supplier × branch group).</summary>
    public int PurchaseOrdersCreated { get; set; }

    /// <summary>Decisions skipped because the product has no default supplier (never fatal).</summary>
    public int SkippedNoSupplier { get; set; }

    /// <summary>Per-PO breakdown.</summary>
    public List<ConsolidatedPoDto> PurchaseOrders { get; set; } = new();
}

/// <summary>One draft purchase order produced by a consolidation run.</summary>
public class ConsolidatedPoDto
{
    public Guid PurchaseOrderId { get; set; }
    public string PONumber { get; set; } = null!;
    public string SupplierName { get; set; } = null!;
    public string BranchName { get; set; } = null!;

    /// <summary>Distinct product lines on the PO.</summary>
    public int LineCount { get; set; }

    /// <summary>Pending decisions consolidated into this PO (may exceed LineCount when merged).</summary>
    public int DecisionCount { get; set; }
}
