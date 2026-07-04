using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Operations.Entities;

public class AppStockTransferItem : Entity<Guid>
{
    public Guid StockTransferId { get; private set; }
    public Guid ProductId { get; private set; }
    public int RequestedQuantity { get; private set; }
    public int? ApprovedQuantity { get; private set; }
    public int? TransferredQuantity { get; private set; }
    /// <summary>
    /// Expiry batches consumed at the source when this item shipped, in the compact
    /// <see cref="StockTransferBatchBreakdown"/> format. Replayed at the destination
    /// on receive so batch expiry dates survive the transfer.
    /// </summary>
    public string? ShippedBatchBreakdown { get; private set; }

    protected AppStockTransferItem() { }

    internal AppStockTransferItem(Guid id, Guid stockTransferId, Guid productId, int requestedQuantity)
        : base(id)
    {
        if (requestedQuantity <= 0)
            throw new BusinessException(OperationsErrorCodes.TransferInvalidQuantity);

        StockTransferId = stockTransferId;
        ProductId = productId;
        RequestedQuantity = requestedQuantity;
    }

    internal void SetApprovedQuantity(int approvedQuantity)
    {
        if (approvedQuantity < 0)
            throw new BusinessException(OperationsErrorCodes.TransferInvalidQuantity);
        ApprovedQuantity = approvedQuantity;
    }

    internal void SetTransferredQuantity(int transferredQuantity)
    {
        if (transferredQuantity < 0)
            throw new BusinessException(OperationsErrorCodes.TransferInvalidQuantity);
        TransferredQuantity = transferredQuantity;
    }

    internal void SetShippedBatchBreakdown(string? breakdown)
    {
        ShippedBatchBreakdown = breakdown;
    }
}
