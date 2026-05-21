using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;

namespace Operations.Entities;

public class AppStockTransfer : FullAuditedAggregateRoot<Guid>
{
    public Guid FromBranchId { get; set; }
    public Guid ToBranchId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime RequestedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? Notes { get; set; }

    public ICollection<AppStockTransferItem> Items { get; private set; }

    protected AppStockTransfer()
    {
        Items = new List<AppStockTransferItem>();
    }

    public AppStockTransfer(
        Guid id,
        Guid fromBranchId,
        Guid toBranchId,
        string status,
        DateTime requestedDate,
        Guid? requestedByUserId = null,
        string? notes = null)
        : base(id)
    {
        FromBranchId = fromBranchId;
        ToBranchId = toBranchId;
        Status = status;
        RequestedDate = requestedDate;
        RequestedByUserId = requestedByUserId;
        Notes = notes;
        Items = new List<AppStockTransferItem>();
    }

    public AppStockTransferItem AddItem(Guid productId, int requestedQty)
    {
        var item = new AppStockTransferItem(Guid.NewGuid(), Id, productId, requestedQty);
        Items.Add(item);
        return item;
    }
}
