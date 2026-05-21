using Volo.Abp.Application.Dtos;

namespace Inventory.Branches;

public class GetBranchesInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public bool? IsActive { get; set; }
}
