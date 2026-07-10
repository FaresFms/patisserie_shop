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

    Task<List<ProductBranchStockDto>> GetProductStockAcrossBranchesAsync(Guid productId);

    Task<List<Guid>> GetAccessibleBranchIdsAsync();

    Task<BranchInventoryDto> InitializeAsync(InitializeBranchInventoryDto input);

    Task<BranchInventoryDto> AdjustStockAsync(Guid id, AdjustStockDto input);

    /// <summary>
    /// One-click write-off of every expired unit currently on hand for this
    /// product+branch. The quantity is recomputed server-side (never trusted from the
    /// client) and removed via a WriteOff movement, which clears expired batches first.
    /// </summary>
    Task<BranchInventoryDto> WriteOffExpiredAsync(Guid id);

    Task<BranchInventoryDto> UpdateLimitsAsync(Guid id, UpdateStockLimitsDto input);

    Task DeleteAsync(Guid id);
}
