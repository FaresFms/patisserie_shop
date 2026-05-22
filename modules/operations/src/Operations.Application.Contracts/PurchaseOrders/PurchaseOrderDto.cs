using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Operations.PurchaseOrders;

public class PurchaseOrderDto : EntityDto<Guid>
{
    public string PONumber { get; set; } = null!;
    public string Status { get; set; } = null!;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public Guid DestBranchId { get; set; }
    public string DestBranchName { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public DateTime? ActualDeliveryDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Notes { get; set; }
    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public string? CreatorUserName { get; set; }
    public int ItemCount { get; set; }
    public List<PurchaseOrderItemDto> Items { get; set; } = new();
}
