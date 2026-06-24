using System;

namespace Operations.PurchaseOrders;

/// <summary>
/// Per-supplier delivery-performance aggregate computed from received/terminal
/// purchase-order history (Status Received or PartialReceived). Backs the host
/// supplier-scorecard read model and the measured-lead-time feedback into the
/// reorder-point formula. All counts/sums are computed in the query so the app
/// service never touches a queryable to aggregate them.
/// </summary>
public class SupplierScorecardRow
{
    public Guid SupplierId { get; set; }

    /// <summary>Purchase orders to this supplier in the window (Received + PartialReceived).</summary>
    public int TotalOrders { get; set; }

    /// <summary>Orders that reached the fully-Received status.</summary>
    public int ReceivedOrders { get; set; }

    /// <summary>
    /// Orders delivered on or before the expected date (both ActualDeliveryDate and
    /// ExpectedDeliveryDate set, Actual ≤ Expected).
    /// </summary>
    public int OnTimeOrders { get; set; }

    /// <summary>
    /// Orders delivered after the expected date (both dates set, Actual &gt; Expected).
    /// </summary>
    public int LateOrders { get; set; }

    /// <summary>
    /// Average (ActualDeliveryDate − ExpectedDeliveryDate) in whole days over orders
    /// where both dates are set. Negative means early on average. Null when no order
    /// in the window has both dates set.
    /// </summary>
    public double? AvgDelayDays { get; set; }

    /// <summary>Total ordered quantity across the matched orders' line items.</summary>
    public int TotalOrderedQty { get; set; }

    /// <summary>Total received quantity across the matched orders' line items.</summary>
    public int TotalReceivedQty { get; set; }

    /// <summary>
    /// Average (ActualDeliveryDate − OrderDate) in whole days over received orders that
    /// have an ActualDeliveryDate. Null when no such order exists — the caller then
    /// falls back to the supplier's configured LeadTimeDays.
    /// </summary>
    public double? AvgActualLeadTimeDays { get; set; }

    /// <summary>
    /// Number of received orders with a delivery date — the sample size behind
    /// <see cref="AvgActualLeadTimeDays"/>. Used to decide whether the measured lead
    /// time is reliable enough to override the configured value.
    /// </summary>
    public int LeadTimeSampleSize { get; set; }
}
