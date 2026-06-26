using System;
using Volo.Abp.Application.Dtos;

namespace Production.Orders;

public class GetProductionOrdersInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public string? Status { get; set; }
    public Guid? KitchenBranchId { get; set; }
}
