using System;
using System.Collections.Generic;

namespace Operations.StockTransfers;

public class AddStockTransferItemDto
{
    public Guid ProductId { get; set; }
    public int RequestedQuantity { get; set; }
}

public class UpdateApprovedQuantityDto
{
    public int ApprovedQuantity { get; set; }
}

public class ShipStockTransferDto
{
    /// <summary>
    /// Per-item quantities actually leaving the source. A missing item defaults to its
    /// approved quantity; each value is clamped to [0, approved] server-side. Empty ships
    /// every item at its approved quantity (the old behaviour).
    /// </summary>
    public List<ShipTransferLineDto> Lines { get; set; } = new();
}

public class ShipTransferLineDto
{
    public Guid ItemId { get; set; }
    public int ShippedQuantity { get; set; }
}

public class CompleteStockTransferDto
{
    public List<CompleteTransferLineDto> Lines { get; set; } = new();
}

public class CompleteTransferLineDto
{
    public Guid ItemId { get; set; }
    public int TransferredQuantity { get; set; }
}

public class RejectStockTransferDto
{
    public string Reason { get; set; } = null!;
}

public class CancelStockTransferDto
{
    public string? Reason { get; set; }
}

/// <summary>
/// How many transfers are waiting for the current user, per workflow step.
/// Powers the notification bell and the "Needs my action" tab.
/// </summary>
public class StockTransferActionSummaryDto
{
    public int DraftsToSubmit { get; set; }
    public int PendingToApprove { get; set; }
    public int ApprovedToShip { get; set; }
    public int InTransitToReceive { get; set; }

    public int Total => DraftsToSubmit + PendingToApprove + ApprovedToShip + InTransitToReceive;
}
