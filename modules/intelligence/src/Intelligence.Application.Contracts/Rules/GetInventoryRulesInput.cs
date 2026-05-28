using Volo.Abp.Application.Dtos;

namespace Intelligence.Rules;

public class GetInventoryRulesInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public string? RuleType { get; set; }
    public bool? IsActive { get; set; }
}
