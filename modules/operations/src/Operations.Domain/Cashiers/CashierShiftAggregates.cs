using System;
using Operations.Entities;

namespace Operations.Cashiers;

/// <summary>
/// Live sales totals for a single cashier shift, computed in one grouped query over the
/// shift's linked sales so the app service never aggregates a queryable. NonVoidedTotal
/// excludes voided sales; VoidedTotal sums voided ones.
/// </summary>
public class ShiftSalesTotals
{
    public Guid ShiftId { get; set; }
    public int SalesCount { get; set; }
    public decimal NonVoidedTotal { get; set; }
    public decimal VoidedTotal { get; set; }
}

/// <summary>
/// A recent sale rung by a cashier, with its line-item count, for the "recent sales"
/// strip on the POS (used to offer a Void button inside the window).
/// </summary>
public class CashierSaleRow
{
    public AppSale Sale { get; set; } = null!;
    public int ItemCount { get; set; }
}
