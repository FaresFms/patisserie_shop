using System;

namespace Production.Orders;

public class ProductionOrderAllocationDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid? BranchProductionRequestId { get; set; }
    public Guid? BranchProductionRequestItemId { get; set; }
    public int AllocatedQuantity { get; set; }
    public int DispatchedQuantity { get; set; }
    public int InTransitQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public int LostQuantity { get; set; }
    public int RemainingToDispatch { get; set; }
}
