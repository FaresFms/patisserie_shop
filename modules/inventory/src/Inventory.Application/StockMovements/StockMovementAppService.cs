using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Inventory.StockMovements;

[Authorize(InventoryPermissions.StockMovements.Default)]
public class StockMovementAppService : InventoryAppService, IStockMovementAppService
{
    private readonly IStockMovementRepository _movementRepository;
    private readonly BranchAccessChecker _branchAccess;

    public StockMovementAppService(
        IStockMovementRepository movementRepository,
        BranchAccessChecker branchAccess)
    {
        _movementRepository = movementRepository;
        _branchAccess = branchAccess;
    }

    public async Task<PagedResultDto<StockMovementDto>> GetListAsync(GetStockMovementsInput input)
    {
        var filter = await BuildFilterAsync(input);

        var totalCount = await _movementRepository.CountFilteredAsync(filter);

        var rows = await _movementRepository.GetFilteredListAsync(
            filter,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        return new PagedResultDto<StockMovementDto>(
            totalCount,
            rows.ConvertAll(MapRow));
    }

    public async Task<StockMovementSummaryDto> GetSummaryAsync(GetStockMovementsInput input)
    {
        var filter = await BuildFilterAsync(input);

        var byType = await _movementRepository.GetCountsByTypeAsync(filter);
        var lookup = byType.ToDictionary(x => x.MovementType, x => x.Count);

        return new StockMovementSummaryDto
        {
            TotalCount = byType.Sum(x => x.Count),
            PurchaseCount = lookup.GetValueOrDefault(StockMovementTypes.Purchase),
            SaleCount = lookup.GetValueOrDefault(StockMovementTypes.Sale),
            TransferInCount = lookup.GetValueOrDefault(StockMovementTypes.TransferIn),
            TransferOutCount = lookup.GetValueOrDefault(StockMovementTypes.TransferOut),
            ManualAdjustmentCount = lookup.GetValueOrDefault(StockMovementTypes.ManualAdjustment),
        };
    }

    private async Task<StockMovementListFilter> BuildFilterAsync(GetStockMovementsInput input)
    {
        var scope = await _branchAccess.GetScopedBranchIdsAsync(InventoryPermissions.StockMovements.ViewAll);

        return new StockMovementListFilter
        {
            BranchId = input.BranchId,
            ProductId = input.ProductId,
            MovementType = input.MovementType,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            Filter = input.Filter,
            BranchIdScope = scope
        };
    }

    private StockMovementDto MapRow(StockMovementWithContext row)
    {
        var dto = ObjectMapper.Map<AppStockMovement, StockMovementDto>(row.Movement);
        dto.BranchName = row.Branch.Name;
        dto.ProductName = row.Product.Name;
        dto.ProductSKU = row.Product.SKU;
        dto.ProductUnit = row.Product.Unit;
        return dto;
    }
}
