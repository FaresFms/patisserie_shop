using System;

namespace Operations.Cashier;

/// <summary>
/// A cash-drawer session for a cashier at a branch, with live (or final) drawer figures.
/// SalesCount / SalesTotal / VoidedTotal / ExpectedCash are computed from the shift's
/// linked sales. CountedCash / ClosedAt / Variance are populated only once the shift is
/// closed.
/// </summary>
public class CashierShiftDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string? BranchName { get; set; }
    public Guid CashierUserId { get; set; }
    public string? CashierUserName { get; set; }
    public DateTime OpenedAt { get; set; }
    public decimal OpeningFloat { get; set; }
    public string Status { get; set; } = CashierShiftStatuses.Open;

    /// <summary>Non-voided sales count rung on this shift.</summary>
    public int SalesCount { get; set; }

    /// <summary>Sum of non-voided sale totals on this shift.</summary>
    public decimal SalesTotal { get; set; }

    /// <summary>Sum of voided sale totals on this shift.</summary>
    public decimal VoidedTotal { get; set; }

    /// <summary>OpeningFloat + non-voided sales total (voided sales never count).</summary>
    public decimal ExpectedCash { get; set; }

    public decimal? CountedCash { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal? Variance { get; set; }
}
