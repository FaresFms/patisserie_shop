using System;

namespace patisserie_shop.Cashier;

/// <summary>
/// Input for a manual low-stock report raised by a cashier from the POS for a single
/// product at the cashier's current branch. The current on-hand quantity and the
/// product/branch names are resolved server-side, so the cashier only supplies the keys.
/// </summary>
public class ReportLowStockInput
{
    public Guid ProductId { get; set; }

    public Guid BranchId { get; set; }
}
