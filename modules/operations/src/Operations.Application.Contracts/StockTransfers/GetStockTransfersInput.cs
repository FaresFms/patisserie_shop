using System;
using Volo.Abp.Application.Dtos;

namespace Operations.StockTransfers;

public class GetStockTransfersInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public string? Status { get; set; }
    public Guid? FromBranchId { get; set; }
    public Guid? ToBranchId { get; set; }
}
