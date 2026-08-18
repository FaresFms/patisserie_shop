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
    public int PlannedQuantity { get; private set; }
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

        if (FulfilledQuantity + quantity > ApprovedQuantity)
        {
            throw new BusinessException(ProductionErrorCodes.RequestFulfilledQuantityExceeded)
                .WithData("ApprovedQuantity", ApprovedQuantity)
                .WithData("FulfilledQuantity", FulfilledQuantity)
                .WithData("ReceivedQuantity", quantity);
        }

        FulfilledQuantity += quantity;
        PlannedQuantity = Math.Max(PlannedQuantity, FulfilledQuantity);
    }

    internal void ReservePlannedQuantity(int quantity)
    {
        if (quantity <= 0 || PlannedQuantity + quantity > ApprovedQuantity)
        {
            throw new BusinessException(ProductionErrorCodes.RequestPlannedQuantityExceeded)
                .WithData("ApprovedQuantity", ApprovedQuantity)
                .WithData("PlannedQuantity", PlannedQuantity)
                .WithData("RequestedReservation", quantity);
        }

        PlannedQuantity += quantity;
    }

    internal void ReleasePlannedQuantity(int quantity)
    {
        if (quantity <= 0 || PlannedQuantity - quantity < FulfilledQuantity)
        {
            throw new BusinessException(ProductionErrorCodes.RequestPlannedQuantityExceeded)
                .WithData("PlannedQuantity", PlannedQuantity)
                .WithData("FulfilledQuantity", FulfilledQuantity)
                .WithData("RequestedRelease", quantity);
        }

        PlannedQuantity -= quantity;
    }

    internal void CompleteReservedStockDispatch(int shippedQuantity, int receivedQuantity)
    {
        var openReservedQuantity = PlannedQuantity - FulfilledQuantity;
        if (shippedQuantity <= 0
            || receivedQuantity < 0
            || receivedQuantity > shippedQuantity
            || shippedQuantity > openReservedQuantity)
        {
            throw new BusinessException(ProductionErrorCodes.StockDispatchReconciliationMismatch)
                .WithData("ShippedQuantity", shippedQuantity)
                .WithData("ReceivedQuantity", receivedQuantity)
                .WithData("OpenReservedQuantity", openReservedQuantity);
        }

        var lostQuantity = shippedQuantity - receivedQuantity;
        FulfilledQuantity += receivedQuantity;
        PlannedQuantity -= lostQuantity;
    }
}
