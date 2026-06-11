using System.Linq;
using System.Threading.Tasks;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Inventory.StockBatches;

/// <summary>
/// Read-only view over the best-effort expiry/batch ledger. Branch scoping mirrors
/// BranchInventoryAppService: ManageAll sees everything, a branch manager only the
/// branches they manage (via <see cref="BranchAccessChecker"/>).
/// </summary>
[Authorize(InventoryPermissions.BranchInventory.Default)]
public class StockBatchAppService : InventoryAppService, IStockBatchAppService
{
    private readonly IStockBatchRepository _batchRepository;
    private readonly BranchAccessChecker _branchAccess;

    public StockBatchAppService(
        IStockBatchRepository batchRepository,
        BranchAccessChecker branchAccess)
    {
        _batchRepository = batchRepository;
        _branchAccess = branchAccess;
    }

    public async Task<PagedResultDto<StockBatchDto>> GetListAsync(GetStockBatchesInput input)
    {
        if (input.BranchId.HasValue)
        {
            await _branchAccess.EnsureAccessAsync(input.BranchId.Value);
        }

        // null = unrestricted (ManageAll); otherwise the branches the caller manages.
        var branchIdScope = await _branchAccess.GetScopedBranchIdsAsync(
            InventoryPermissions.BranchInventory.ManageAll);

        var totalCount = await _batchRepository.CountWithDetailsAsync(
            input.Filter, input.BranchId, input.IncludeDepleted, branchIdScope);

        var rows = await _batchRepository.GetListWithDetailsAsync(
            input.Filter, input.BranchId, input.IncludeDepleted, branchIdScope,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        return new PagedResultDto<StockBatchDto>(totalCount, [.. rows.Select(Project)]);
    }

    private StockBatchDto Project(StockBatchWithDetails row)
    {
        var dto = ObjectMapper.Map<AppStockBatch, StockBatchDto>(row.Batch);
        dto.ProductName = row.Product.Name;
        dto.ProductSKU = row.Product.SKU;
        dto.ProductUnit = row.Product.Unit;
        dto.BranchName = row.Branch.Name;
        return dto;
    }
}
