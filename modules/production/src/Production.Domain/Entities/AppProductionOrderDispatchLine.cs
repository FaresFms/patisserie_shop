using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Production.Entities;

public class AppProductionOrderDispatchLine : Entity<Guid>
{
    public Guid ProductionOrderDispatchId { get; private set; }
    public Guid ProductionOrderAllocationId { get; private set; }
    public Guid? BranchProductionRequestId { get; private set; }
    public Guid? BranchProductionRequestItemId { get; private set; }
    public int ShippedQuantity { get; private set; }
    public int ReceivedQuantity { get; private set; }
    public int LostQuantity { get; private set; }

    protected AppProductionOrderDispatchLine() { }

    internal AppProductionOrderDispatchLine(
        Guid id,
        Guid productionOrderDispatchId,
        Guid productionOrderAllocationId,
        Guid? branchProductionRequestId,
        Guid? branchProductionRequestItemId,
        int shippedQuantity)
        : base(id)
    {
        if (shippedQuantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity)
                .WithData("ShippedQuantity", shippedQuantity);
        }

        ProductionOrderDispatchId = productionOrderDispatchId;
        ProductionOrderAllocationId = productionOrderAllocationId;
        BranchProductionRequestId = branchProductionRequestId;
        BranchProductionRequestItemId = branchProductionRequestItemId;
        ShippedQuantity = shippedQuantity;
    }

    internal void Complete(int receivedQuantity, int lostQuantity)
    {
        if (receivedQuantity < 0 || lostQuantity < 0
            || receivedQuantity + lostQuantity != ShippedQuantity)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity);
        }

        ReceivedQuantity = receivedQuantity;
        LostQuantity = lostQuantity;
    }
}
