using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Production.Entities;

public class AppProductionOrderAllocation : Entity<Guid>
{
    public Guid ProductionOrderId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid? BranchProductionRequestId { get; private set; }
    public Guid? BranchProductionRequestItemId { get; private set; }
    public int AllocatedQuantity { get; private set; }
    public int DispatchedQuantity { get; private set; }
    public int ReceivedQuantity { get; private set; }
    public int LostQuantity { get; private set; }

    public int RemainingToDispatch => Math.Max(0, AllocatedQuantity - DispatchedQuantity);
    public int InTransitQuantity => Math.Max(0, DispatchedQuantity - ReceivedQuantity - LostQuantity);

    protected AppProductionOrderAllocation() { }

    internal AppProductionOrderAllocation(
        Guid id,
        Guid productionOrderId,
        Guid branchId,
        Guid? branchProductionRequestId,
        Guid? branchProductionRequestItemId,
        int allocatedQuantity)
        : base(id)
    {
        ProductionOrderId = productionOrderId;
        BranchId = branchId;
        BranchProductionRequestId = branchProductionRequestId;
        BranchProductionRequestItemId = branchProductionRequestItemId;
        SetAllocatedQuantity(allocatedQuantity);
    }

    internal void MarkDispatched(int quantity)
    {
        if (quantity <= 0 || quantity > RemainingToDispatch)
        {
            throw new BusinessException(ProductionErrorCodes.DispatchQuantityExceedsRemaining)
                .WithData("AllocatedQuantity", AllocatedQuantity)
                .WithData("DispatchedQuantity", DispatchedQuantity)
                .WithData("RequestedDispatch", quantity);
        }

        DispatchedQuantity += quantity;
    }

    internal void RecordTransferResult(int receivedQuantity, int lostQuantity)
    {
        if (receivedQuantity < 0 || lostQuantity < 0
            || receivedQuantity + lostQuantity > InTransitQuantity)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity)
                .WithData("ReceivedQuantity", receivedQuantity)
                .WithData("LostQuantity", lostQuantity)
                .WithData("InTransitQuantity", InTransitQuantity);
        }

        ReceivedQuantity += receivedQuantity;
        LostQuantity += lostQuantity;
    }

    internal int ReleaseUnshippedQuantity(int quantity)
    {
        if (quantity <= 0 || quantity > RemainingToDispatch)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity)
                .WithData("RequestedRelease", quantity)
                .WithData("RemainingToDispatch", RemainingToDispatch);
        }

        AllocatedQuantity -= quantity;
        return quantity;
    }

    private void SetAllocatedQuantity(int allocatedQuantity)
    {
        if (allocatedQuantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity)
                .WithData("AllocatedQuantity", allocatedQuantity);
        }

        AllocatedQuantity = allocatedQuantity;
    }
}
