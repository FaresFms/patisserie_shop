using System;

namespace Production.BranchRequests;

public class BranchProductionRequestFulfillmentTarget
{
    public Guid RequestId { get; set; }
    public Guid RequestItemId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public DateTime NeededByDate { get; set; }
    public int RemainingQuantity { get; set; }
}
