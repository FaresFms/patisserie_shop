using System;
using System.Collections.Generic;

namespace Operations.StockTransfers;

/// <summary>
/// Describes which transfers are "waiting for the current user" so the repository
/// can filter them in one place. The application layer resolves permissions and
/// managed branches; the repository only translates this spec into a predicate:
///   Draft      → requested by me, or destined to a branch I manage;
///   Pending    → I can approve;
///   Approved   → source branch is mine (I ship), or I manage all branches;
///   InTransit  → destination branch is mine (I receive), or I manage all branches.
/// </summary>
public class StockTransferActionSpec
{
    public Guid? UserId { get; set; }
    public bool CanApprove { get; set; }
    public bool ManageAllBranches { get; set; }
    public List<Guid> ManagedBranchIds { get; set; } = new();
}

public class StockTransferVisibilitySpec
{
    public bool CanViewAll { get; set; }
    public List<Guid> ManagedBranchIds { get; set; } = new();
}

/// <summary>Per-bucket counts for the action spec — feeds the notification bell and tabs.</summary>
public class StockTransferActionCounts
{
    public int DraftsToSubmit { get; set; }
    public int PendingToApprove { get; set; }
    public int ApprovedToShip { get; set; }
    public int InTransitToReceive { get; set; }
}
