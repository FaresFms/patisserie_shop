using Volo.Abp.Application.Dtos;

namespace Inventory.Suppliers;

public class GetSuppliersInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public bool? IsActive { get; set; }
}
