using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Intelligence.Rules;

public interface IInventoryRuleAppService : IApplicationService
{
    Task<InventoryRuleDto> GetAsync(Guid id);

    Task<PagedResultDto<InventoryRuleDto>> GetListAsync(GetInventoryRulesInput input);

    Task<InventoryRuleDto> CreateAsync(CreateInventoryRuleDto input);

    Task<InventoryRuleDto> UpdateAsync(Guid id, UpdateInventoryRuleDto input);

    Task DeleteAsync(Guid id);
}
