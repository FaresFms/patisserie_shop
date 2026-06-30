using System;
using Volo.Abp.Application.Dtos;

namespace Production.Waste;

public class GetProductionWastesInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public string? WasteType { get; set; }
    public Guid? KitchenBranchId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
