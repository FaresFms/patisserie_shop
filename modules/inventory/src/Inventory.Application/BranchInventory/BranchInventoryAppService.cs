using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Inventory.BranchInventory;

[Authorize(InventoryPermissions.BranchInventory.Default)]
public class BranchInventoryAppService : InventoryAppService, IBranchInventoryAppService
{
    private readonly IBranchInventoryRepository _inventoryRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly BranchInventoryManager _manager;
    private readonly BranchAccessChecker _branchAccess;

    public BranchInventoryAppService(
        IBranchInventoryRepository inventoryRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository,
        BranchInventoryManager manager,
        BranchAccessChecker branchAccess)
    {
        _inventoryRepository = inventoryRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _manager = manager;
        _branchAccess = branchAccess;
    }

    public async Task<BranchInventoryDto> GetAsync(Guid id)
    {
        var row = await _inventoryRepository.GetWithProductAsync(id);
        await _branchAccess.EnsureAccessAsync(row.Inventory.BranchId);
        return Project(row);
    }

    public async Task<PagedResultDto<BranchInventoryDto>> GetListAsync(GetBranchInventoryInput input)
    {
        if (input.BranchId == Guid.Empty)
        {
            throw new BusinessException(InventoryErrorCodes.BranchIdRequired);
        }
        await _branchAccess.EnsureAccessAsync(input.BranchId);

        var totalCount = await _inventoryRepository.CountWithProductAsync(
            input.BranchId, input.Filter,
            input.OnlyOutOfStock == true,
            input.OnlyLowStock == true,
            input.IncludeInactiveProducts == true);

        var rows = await _inventoryRepository.GetListWithProductAsync(
            input.BranchId, input.Filter,
            input.OnlyOutOfStock == true,
            input.OnlyLowStock == true,
            input.IncludeInactiveProducts == true,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        return new PagedResultDto<BranchInventoryDto>(totalCount, [.. rows.Select(Project)]);
    }

    public async Task<BranchInventoryStatsDto> GetStatsAsync(Guid branchId)
    {
        await _branchAccess.EnsureAccessAsync(branchId);

        var snapshots = await _inventoryRepository.GetActiveStockSnapshotsAsync(branchId);

        var stats = new BranchInventoryStatsDto
        {
            TotalItems = snapshots.Count,
            OutOfStockCount = snapshots.Count(r => r.QuantityOnHand <= 0),
            LowStockCount = snapshots.Count(r => r.QuantityOnHand > 0 && r.QuantityOnHand <= r.MinimumStock),
        };
        stats.HealthyStockCount = stats.TotalItems - stats.LowStockCount - stats.OutOfStockCount;
        return stats;
    }

    public async Task<List<ProductBranchStockDto>> GetProductStockAcrossBranchesAsync(Guid productId)
    {
        var product = await _productRepository.GetAsync(productId);

        // Branch isolation: a ManageAll caller (admin) sees every branch (null scope);
        // a branch manager only sees the branches they manage. Matches the scoping
        // every other read method on this service applies.
        var scope = await _branchAccess.GetScopedBranchIdsAsync(InventoryPermissions.BranchInventory.ManageAll);
        var rows = await _inventoryRepository.GetByProductAsync(productId, scope);

        return [.. rows.Select(row => new ProductBranchStockDto
        {
            BranchId = row.Inventory.BranchId,
            BranchName = row.Branch.Name,
            QuantityOnHand = row.Inventory.QuantityOnHand,
            MinimumStock = row.Inventory.MinimumStock,
            MaximumStock = row.Inventory.MaximumStock,
            ProductUnit = product.Unit
        })];
    }

    public Task<List<Guid>> GetAccessibleBranchIdsAsync()
        => _branchAccess.GetAccessibleBranchIdsAsync();

    [Authorize(InventoryPermissions.BranchInventory.Initialize)]
    public async Task<BranchInventoryDto> InitializeAsync(InitializeBranchInventoryDto input)
    {
        await _branchAccess.EnsureAccessAsync(input.BranchId);
        await _branchRepository.GetAsync(input.BranchId);
        await _productRepository.GetAsync(input.ProductId);

        var entity = await _manager.InitializeAsync(
            input.BranchId,
            input.ProductId,
            input.InitialQuantity,
            input.MinimumStock,
            input.MaximumStock);

        await _inventoryRepository.InsertAsync(entity, autoSave: true);
        return await ProjectAsync(entity);
    }

    [Authorize(InventoryPermissions.BranchInventory.Adjust)]
    public async Task<BranchInventoryDto> AdjustStockAsync(Guid id, AdjustStockDto input)
    {
        var inv = await _inventoryRepository.GetAsync(id);
        await _branchAccess.EnsureAccessAsync(inv.BranchId);

        BranchInventoryManager.EnsureConcurrencyStamp(inv, input.ConcurrencyStamp);

        await _manager.AdjustStockAsync(inv, input.NewQuantity, input.MovementType, input.Notes);
        await _inventoryRepository.UpdateAsync(inv, autoSave: true);
        return await ProjectAsync(inv);
    }

    [Authorize(InventoryPermissions.BranchInventory.SetLimits)]
    public async Task<BranchInventoryDto> UpdateLimitsAsync(Guid id, UpdateStockLimitsDto input)
    {
        var inv = await _inventoryRepository.GetAsync(id);
        await _branchAccess.EnsureAccessAsync(inv.BranchId);

        _manager.UpdateLimits(inv, input.MinimumStock, input.MaximumStock);
        await _inventoryRepository.UpdateAsync(inv, autoSave: true);
        return await ProjectAsync(inv);
    }

    [Authorize(InventoryPermissions.BranchInventory.ManageAll)]
    public async Task DeleteAsync(Guid id)
    {
        await _inventoryRepository.DeleteAsync(id);
    }

    private async Task<BranchInventoryDto> ProjectAsync(AppBranchInventory inv)
    {
        var product = await _productRepository.GetAsync(inv.ProductId);
        return Project(new BranchInventoryWithProduct { Inventory = inv, Product = product });
    }

    private BranchInventoryDto Project(BranchInventoryWithProduct row)
    {
        var dto = ObjectMapper.Map<AppBranchInventory, BranchInventoryDto>(row.Inventory);
        dto.ProductName = row.Product.Name;
        dto.ProductSKU = row.Product.SKU;
        dto.ProductUnit = row.Product.Unit;
        dto.ProductIsActive = row.Product.IsActive;
        return dto;
    }
}
