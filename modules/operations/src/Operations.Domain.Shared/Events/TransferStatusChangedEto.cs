using System;

namespace Operations.Events;

/// <summary>
/// Raised by <c>AppStockTransfer</c> on every workflow transition
/// (Submit, Approve, Ship, Complete, Reject, Cancel) so the UI layer can
/// refresh "needs my action" counters and notify the party whose turn it is.
/// </summary>
[Serializable]
public class TransferStatusChangedEto
{
    public Guid TransferId { get; set; }
    public Guid? FromBranchId { get; set; }
    public Guid ToBranchId { get; set; }
    public string OldStatus { get; set; } = null!;
    public string NewStatus { get; set; } = null!;
}
