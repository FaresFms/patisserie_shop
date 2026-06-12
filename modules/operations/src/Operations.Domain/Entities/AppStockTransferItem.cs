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
}
