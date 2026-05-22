using System;
using Volo.Abp.Application.Dtos;

namespace Operations.PurchaseOrders;

public class GetPurchaseOrdersInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public string? Status { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? DestBranchId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
