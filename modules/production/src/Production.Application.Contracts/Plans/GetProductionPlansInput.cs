using System;
using Volo.Abp.Application.Dtos;

namespace Production.Plans;

public class GetProductionPlansInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public string? Status { get; set; }
    public Guid? KitchenBranchId { get; set; }
}
