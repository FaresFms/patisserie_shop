using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Production.Entities;

public class AppBranchProductionRequestItem : Entity<Guid>
{
    public Guid RequestId { get; private set; }
    public Guid ProductId { get; private set; }
    public int RequestedQuantity { get; private set; }
    public int ApprovedQuantity { get; private set; }
    public int FulfilledQuantity { get; private set; }
    public string? Notes { get; private set; }

    protected AppBranchProductionRequestItem() { }

    internal AppBranchProductionRequestItem(
        Guid id,
        Guid requestId,
        Guid productId,
        int requestedQuantity,
        string? notes = null)
        : base(id)
    {
        RequestId = requestId;
        ProductId = productId;
        SetRequestedQuantity(requestedQuantity);
        Notes = notes;
    }

    internal void SetRequestedQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidRequestQuantity)
                .WithData("Quantity", quantity);
        }

        RequestedQuantity = quantity;
    }

    internal void SetNotes(string? notes) => Notes = notes;

    internal void Approve(int quantity)
    {
        if (quantity < 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidRequestQuantity)
                .WithData("Quantity", quantity);
        }

        ApprovedQuantity = quantity;
    }

    internal void AddFulfilledQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidRequestQuantity)
                .WithData("Quantity", quantity);
        }

        FulfilledQuantity = Math.Min(ApprovedQuantity, FulfilledQuantity + quantity);
    }
}
