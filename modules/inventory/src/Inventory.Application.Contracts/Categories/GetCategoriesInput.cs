using Volo.Abp.Application.Dtos;

namespace Inventory.Categories;

public class GetCategoriesInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public bool? IsActive { get; set; }
}
