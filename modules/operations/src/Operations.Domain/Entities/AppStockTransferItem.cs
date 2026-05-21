using System;
using Volo.Abp.Domain.Entities;

namespace Operations.Entities;

public class AppStockTransferItem : Entity<Guid>
{
    public Guid StockTransferId { get; set; }
    public Guid ProductId { get; set; }
    public int RequestedQuantity { get; set; }
    public int? ApprovedQuantity { get; set; }
    public int? TransferredQuantity { get; set; }

    protected AppStockTransferItem() { }

    public AppStockTransferItem(Guid id, Guid stockTransferId, Guid productId, int requestedQuantity)
        : base(id)
    {
        StockTransferId = stockTransferId;
        ProductId = productId;
        RequestedQuantity = requestedQuantity;
    }
}
