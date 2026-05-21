using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Inventory.BranchInventory;

public interface IBranchInventoryAppService : IApplicationService
{
    Task<BranchInventoryDto> GetAsync(Guid id);

    Task<PagedResultDto<BranchInventoryDto>> GetListAsync(GetBranchInventoryInput input);

    Task<BranchInventoryStatsDto> GetStatsAsync(Guid branchId);

    Task<List<Guid>> GetAccessibleBranchIdsAsync();

    Task<BranchInventoryDto> InitializeAsync(InitializeBranchInventoryDto input);

    Task<BranchInventoryDto> AdjustStockAsync(Guid id, AdjustStockDto input);

    Task<BranchInventoryDto> UpdateLimitsAsync(Guid id, UpdateStockLimitsDto input);

    Task DeleteAsync(Guid id);
}
