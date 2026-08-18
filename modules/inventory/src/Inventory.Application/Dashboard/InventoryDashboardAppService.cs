using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.BranchInventory;
using Inventory.Categories;
using Inventory.Entities;
using Inventory.Localization;
using Inventory.Permissions;
using Inventory.StockMovements;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;

namespace Inventory.Dashboard;

[Authorize(InventoryPermissions.BranchInventory.Default)]
public class InventoryDashboardAppService : InventoryAppService, IInventoryDashboardAppService
{
    private const int CriticalItemsLimit = 10;
    private const int RecentMovementsLimit = 8;
    private const int MovementWindowDays = 30;

    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IRepository<AppSupplier, Guid> _supplierRepository;
    private readonly IBranchInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _movementRepository;
    private readonly BranchAccessChecker _branchAccess;

    public InventoryDashboardAppService(
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppBranch, Guid> branchRepository,
        ICategoryRepository categoryRepository,
        IRepository<AppSupplier, Guid> supplierRepository,
        IBranchInventoryRepository inventoryRepository,
        IStockMovementRepository movementRepository,
        BranchAccessChecker branchAccess)
    {
        _productRepository = productRepository;
        _branchRepository = branchRepository;
        _categoryRepository = categoryRepository;
        _supplierRepository = supplierRepository;
        _inventoryRepository = inventoryRepository;
        _movementRepository = movementRepository;
        _branchAccess = branchAccess;
    }

    public async Task<InventoryDashboardDto> GetAsync()
    {
        var scope = await _branchAccess.GetScopedBranchIdsAsync(InventoryPermissions.BranchInventory.ManageAll);

        var totalProducts = await _productRepository.CountAsync();
        var activeProducts = await _productRepository.CountAsync(p => p.IsActive);
        var totalCategories = await _categoryRepository.CountAsync();
        var totalSuppliers = await _supplierRepository.CountAsync();

        var branches = scope == null
            ? await _branchRepository.GetListAsync()
            : await _branchRepository.GetListAsync(b => scope.Contains(b.Id));

        var stockRows = await _inventoryRepository.GetActiveStockRowsAsync(scope);

        var since = Clock.Now.AddDays(-MovementWindowDays);
        var movementRows = await _movementRepository.GetRecentWithContextAsync(scope, since);

        return new InventoryDashboardDto
        {
            TotalProducts = totalProducts,
            ActiveProducts = activeProducts,
            TotalBranches = branches.Count,
            ActiveBranches = branches.Count(b => b.IsActive),
            TotalCategories = totalCategories,
            TotalSuppliers = totalSuppliers,

            TotalInventoryItems = stockRows.Count,
            HealthyStockCount = stockRows.Count(r => !r.Inventory.IsLowStock),
            LowStockCount = stockRows.Count(r => !r.Inventory.IsOutOfStock && r.Inventory.IsLowStock),
            OutOfStockCount = stockRows.Count(r => r.Inventory.IsOutOfStock),

            BranchStats = BuildBranchStats(branches, stockRows),
            CriticalStockItems = BuildCriticalItems(stockRows),
            MovementSummary = BuildMovementSummary(movementRows),
            RecentMovements = BuildRecentMovements(movementRows),
        };
    }

    private static List<BranchDashboardStatsDto> BuildBranchStats(
        List<AppBranch> branches,
        List<InventoryStockRow> stockRows)
    {
        var byBranch = stockRows.GroupBy(r => r.Inventory.BranchId).ToDictionary(g => g.Key, g => g.ToList());

        return branches
            .Select(b =>
            {
                var rows = byBranch.TryGetValue(b.Id, out var list) ? list : new List<InventoryStockRow>();
                var bOut = rows.Count(r => r.Inventory.IsOutOfStock);
                var bLow = rows.Count(r => !r.Inventory.IsOutOfStock && r.Inventory.IsLowStock);
                return new BranchDashboardStatsDto
                {
                    BranchId = b.Id,
                    BranchName = LocalizedBusinessText.Select(b.NameAr, b.NameEn),
                    IsActive = b.IsActive,
                    TotalItems = rows.Count,
                    OutOfStockCount = bOut,
                    LowStockCount = bLow,
                    HealthyStockCount = rows.Count - bOut - bLow
                };
            })
            .OrderByDescending(b => b.TotalItems)
            .ToList();
    }

    private List<BranchInventoryDto> BuildCriticalItems(List<InventoryStockRow> stockRows)
    {
        return stockRows
            .Where(r => r.Inventory.IsLowStock)
            .OrderBy(r => r.Inventory.QuantityOnHand)
            .Take(CriticalItemsLimit)
            .Select(r =>
            {
                var dto = ObjectMapper.Map<AppBranchInventory, BranchInventoryDto>(r.Inventory);
                dto.ProductName = LocalizedBusinessText.Select(r.Product.NameAr, r.Product.NameEn);
                dto.ProductSKU = r.Product.SKU;
                dto.ProductUnit = LocalizedBusinessText.Select(r.Product.UnitAr, r.Product.UnitEn);
                dto.ProductIsActive = r.Product.IsActive;
                return dto;
            })
            .ToList();
    }

    private static StockMovementSummaryDto BuildMovementSummary(List<StockMovementWithContext> rows)
    {
        return new StockMovementSummaryDto
        {
            TotalCount = rows.Count,
            PurchaseCount = rows.Count(r => r.Movement.MovementType == StockMovementTypes.Purchase),
            SaleCount = rows.Count(r => r.Movement.MovementType == StockMovementTypes.Sale),
            TransferInCount = rows.Count(r => r.Movement.MovementType == StockMovementTypes.TransferIn),
            TransferOutCount = rows.Count(r => r.Movement.MovementType == StockMovementTypes.TransferOut),
            ManualAdjustmentCount = rows.Count(r => r.Movement.MovementType == StockMovementTypes.ManualAdjustment)
        };
    }

    private List<StockMovementDto> BuildRecentMovements(List<StockMovementWithContext> rows)
    {
        return rows
            .Take(RecentMovementsLimit)
            .Select(r =>
            {
                var dto = ObjectMapper.Map<AppStockMovement, StockMovementDto>(r.Movement);
                dto.BranchName = LocalizedBusinessText.Select(r.Branch.NameAr, r.Branch.NameEn);
                dto.ProductName = LocalizedBusinessText.Select(r.Product.NameAr, r.Product.NameEn);
                dto.ProductSKU = r.Product.SKU;
                dto.ProductUnit = LocalizedBusinessText.Select(r.Product.UnitAr, r.Product.UnitEn);
                return dto;
            })
            .ToList();
    }
}
