using System;

namespace patisserie_shop.Suppliers;

/// <summary>
/// Supplier delivery scorecard composed in the host from Operations PO history and
/// the Inventory supplier record. Scorecards are supplier-global (not branch-scoped):
/// they carry no per-branch secrets, so any caller with Suppliers.Default may read them.
/// </summary>
public class SupplierScorecardDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>The supplier's configured lead time (AppSupplier.LeadTimeDays).</summary>
    public int ConfiguredLeadTimeDays { get; set; }

    // ── Raw counts (over the analysis window, Received + PartialReceived POs) ──
    public int TotalOrders { get; set; }
    public int ReceivedOrders { get; set; }
    public int OnTimeOrders { get; set; }
    public int LateOrders { get; set; }
    public int TotalOrderedQty { get; set; }
    public int TotalReceivedQty { get; set; }

    /// <summary>
    /// On-time delivery rate as a percentage (0–100) over orders with both expected
    /// and actual delivery dates. Null when no order in the window has both dates.
    /// </summary>
    public double? OnTimeRate { get; set; }

    /// <summary>
    /// Fill rate as a percentage (0–100) = received qty / ordered qty. Null when
    /// nothing was ordered in the window.
    /// </summary>
    public double? FillRate { get; set; }

    /// <summary>
    /// Average delivery delay in days (actual − expected, rounded to one decimal).
    /// Negative means early on average. Null when no order has both dates.
    /// </summary>
    public double? AvgDelayDays { get; set; }

    /// <summary>
    /// Measured lead time in whole days (rounded average of actual − order date over
    /// received orders). Null when there is no received-with-date history.
    /// </summary>
    public int? MeasuredLeadTimeDays { get; set; }

    /// <summary>Sample size behind <see cref="MeasuredLeadTimeDays"/>.</summary>
    public int LeadTimeSampleSize { get; set; }

    /// <summary>
    /// Letter grade A–D derived deterministically from OnTimeRate and FillRate, or
    /// "N/A" when there isn't enough history to grade. See <c>SupplierGrading</c>.
    /// </summary>
    public string Grade { get; set; } = SupplierGrading.NotAvailable;
}
