using System;
using Volo.Abp.Application.Dtos;

namespace Inventory.BranchInventory;

public class GetBranchInventoryInput : PagedAndSortedResultRequestDto
{
    public Guid BranchId { get; set; }
    public string? Filter { get; set; }
    public bool? OnlyLowStock { get; set; }
    public bool? OnlyOutOfStock { get; set; }
    public bool? IncludeInactiveProducts { get; set; }
}
