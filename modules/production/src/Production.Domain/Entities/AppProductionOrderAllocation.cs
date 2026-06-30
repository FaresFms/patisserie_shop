using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Production.Entities;

public class AppProductionOrderAllocation : Entity<Guid>
{
    public Guid ProductionOrderId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid? BranchProductionRequestItemId { get; private set; }
    public int AllocatedQuantity { get; private set; }
    public int FulfilledQuantity { get; private set; }

    protected AppProductionOrderAllocation() { }

    internal AppProductionOrderAllocation(
        Guid id,
        Guid productionOrderId,
        Guid branchId,
        Guid? branchProductionRequestItemId,
        int allocatedQuantity)
        : base(id)
    {
        ProductionOrderId = productionOrderId;
        BranchId = branchId;
        BranchProductionRequestItemId = branchProductionRequestItemId;
        SetAllocatedQuantity(allocatedQuantity);
    }

    internal void AddFulfilledQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity)
                .WithData("Quantity", quantity);
        }

        FulfilledQuantity = Math.Min(AllocatedQuantity, FulfilledQuantity + quantity);
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
