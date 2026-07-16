using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Production.Entities;

public class AppProductionOrderDispatch : Entity<Guid>
{
    public Guid ProductionOrderId { get; private set; }
    public Guid StockTransferId { get; private set; }
    public Guid DestinationBranchId { get; private set; }
    public int ShippedQuantity { get; private set; }
    public int ReceivedQuantity { get; private set; }
    public int LostQuantity { get; private set; }
    public DateTime DispatchedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private readonly List<AppProductionOrderDispatchLine> _lines = new();
    public IReadOnlyCollection<AppProductionOrderDispatchLine> Lines =>
        new ReadOnlyCollection<AppProductionOrderDispatchLine>(_lines);

    public bool IsCompleted => CompletedAt.HasValue;

    protected AppProductionOrderDispatch() { }

    internal AppProductionOrderDispatch(
        Guid id,
        Guid productionOrderId,
        Guid stockTransferId,
        Guid destinationBranchId,
        int shippedQuantity,
        DateTime dispatchedAt)
        : base(id)
    {
        if (shippedQuantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity)
                .WithData("ShippedQuantity", shippedQuantity);
        }

        ProductionOrderId = productionOrderId;
        StockTransferId = stockTransferId;
        DestinationBranchId = destinationBranchId;
        ShippedQuantity = shippedQuantity;
        DispatchedAt = dispatchedAt;
    }

    internal void AddLine(
        Guid lineId,
        Guid productionOrderAllocationId,
        Guid? branchProductionRequestId,
        Guid? branchProductionRequestItemId,
        int shippedQuantity)
    {
        if (_lines.Any(x => x.ProductionOrderAllocationId == productionOrderAllocationId))
        {
            throw new BusinessException(ProductionErrorCodes.ProductionDispatchAlreadyExists)
                .WithData("ProductionOrderAllocationId", productionOrderAllocationId);
        }

        _lines.Add(new AppProductionOrderDispatchLine(
            lineId,
            Id,
            productionOrderAllocationId,
            branchProductionRequestId,
            branchProductionRequestItemId,
            shippedQuantity));
    }

    internal IReadOnlyList<DispatchReceiptLine> Complete(int receivedQuantity, DateTime completedAt)
    {
        if (IsCompleted)
        {
            return Array.Empty<DispatchReceiptLine>();
        }
        if (receivedQuantity < 0 || receivedQuantity > ShippedQuantity)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity)
                .WithData("ReceivedQuantity", receivedQuantity)
                .WithData("ShippedQuantity", ShippedQuantity);
        }

        var remainingReceived = receivedQuantity;
        var results = new List<DispatchReceiptLine>(_lines.Count);
        foreach (var line in _lines)
        {
            var lineReceived = Math.Min(remainingReceived, line.ShippedQuantity);
            var lineLost = line.ShippedQuantity - lineReceived;
            line.Complete(lineReceived, lineLost);
            remainingReceived -= lineReceived;

            results.Add(new DispatchReceiptLine(
                line.ProductionOrderAllocationId,
                line.BranchProductionRequestItemId,
                lineReceived,
                lineLost));
        }

        ReceivedQuantity = receivedQuantity;
        LostQuantity = ShippedQuantity - receivedQuantity;
        CompletedAt = completedAt;
        return results;
    }

    public record DispatchReceiptLine(
        Guid ProductionOrderAllocationId,
        Guid? BranchProductionRequestItemId,
        int ReceivedQuantity,
        int LostQuantity);
}
