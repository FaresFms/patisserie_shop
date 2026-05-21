using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Inventory.Branches;

public interface IBranchAppService : IApplicationService
{
    Task<BranchDto> GetAsync(Guid id);

    Task<PagedResultDto<BranchDto>> GetListAsync(GetBranchesInput input);

    Task<List<BranchLookupDto>> GetLookupAsync();

    Task<BranchDto> CreateAsync(CreateBranchDto input);

    Task<BranchDto> UpdateAsync(Guid id, UpdateBranchDto input);

    Task DeleteAsync(Guid id);
}
