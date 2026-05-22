using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Permissions;
using Inventory.StockMovements;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;

namespace Inventory.Dashboard;

[Authorize(InventoryPermissions.BranchInventory.Default)]
public class InventoryDashboardAppService : InventoryAppService, IInventoryDashboardAppService
{
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppCategory, Guid> _categoryRepository;
    private readonly IRepository<AppSupplier, Guid> _supplierRepository;
    private readonly IRepository<AppBranchInventory, Guid> _inventoryRepository;
    private readonly IRepository<AppStockMovement, Guid> _movementRepository;

    public InventoryDashboardAppService(
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppCategory, Guid> categoryRepository,
        IRepository<AppSupplier, Guid> supplierRepository,
        IRepository<AppBranchInventory, Guid> inventoryRepository,
        IRepository<AppStockMovement, Guid> movementRepository)
    {
        _productRepository = productRepository;
        _branchRepository = branchRepository;
        _categoryRepository = categoryRepository;
        _supplierRepository = supplierRepository;
        _inventoryRepository = inventoryRepository;
        _movementRepository = movementRepository;
    }

    public async Task<InventoryDashboardDto> GetAsync()
    {
        var accessibleBranchIds = await GetAccessibleBranchIdsAsync();

        var productsQ = await _productRepository.GetQueryableAsync();
        var branchesQ = await _branchRepository.GetQueryableAsync();
        var inventoryQ = await _inventoryRepository.GetQueryableAsync();
        var productsForJoin = await _productRepository.GetQueryableAsync();
        var movementsQ = await _movementRepository.GetQueryableAsync();

        // Scope inventory and movements to accessible branches
        if (accessibleBranchIds != null)
        {
            var set = accessibleBranchIds.ToHashSet();
            inventoryQ = inventoryQ.Where(i => set.Contains(i.BranchId));
            movementsQ = movementsQ.Where(m => set.Contains(m.BranchId));
            branchesQ = branchesQ.Where(b => set.Contains(b.Id));
        }

        // KPI counts
        var totalProducts = await AsyncExecuter.CountAsync(productsQ);
        var activeProducts = await AsyncExecuter.CountAsync(productsQ.Where(p => p.IsActive));
        var allBranches = await AsyncExecuter.ToListAsync(branchesQ.Select(b => new { b.Id, b.Name, b.IsActive }));
        var totalCategories = await AsyncExecuter.CountAsync(await _categoryRepository.GetQueryableAsync());
        var totalSuppliers = await AsyncExecuter.CountAsync(await _supplierRepository.GetQueryableAsync());

        // Stock health across all accessible branches (active products only)
        var invRows = await AsyncExecuter.ToListAsync(
            from inv in inventoryQ
            join p in productsForJoin on inv.ProductId equals p.Id
            where p.IsActive
            select new
            {
                inv.BranchId,
                inv.QuantityOnHand,
                inv.MinimumStock,
                p.Name,
                p.SKU,
                p.Unit,
                inv.Id,
                inv.ProductId,
                inv.MaximumStock,
                inv.LastRestockedDate,
                inv.LastSoldDate,
                inv.ConcurrencyStamp
            });

        var totalItems = invRows.Count;
        var outOfStock = invRows.Where(r => r.QuantityOnHand <= 0).ToList();
        var lowStock = invRows.Where(r => r.QuantityOnHand > 0 && r.QuantityOnHand <= r.MinimumStock).ToList();
        var healthyStock = invRows.Where(r => r.QuantityOnHand > r.MinimumStock).ToList();

        // Per-branch stats
        var branchStats = allBranches.Select(b =>
        {
            var branchRows = invRows.Where(r => r.BranchId == b.Id).ToList();
            var bTotal = branchRows.Count;
            var bOut = branchRows.Count(r => r.QuantityOnHand <= 0);
            var bLow = branchRows.Count(r => r.QuantityOnHand > 0 && r.QuantityOnHand <= r.MinimumStock);
            return new BranchDashboardStatsDto
            {
                BranchId = b.Id,
                BranchName = b.Name,
                IsActive = b.IsActive,
                TotalItems = bTotal,
                OutOfStockCount = bOut,
                LowStockCount = bLow,
                HealthyStockCount = bTotal - bOut - bLow
            };
        }).OrderByDescending(b => b.TotalItems).ToList();

        // Critical stock items (out of stock first, then low stock, top 10)
        var criticalRows = outOfStock.Concat(lowStock)
            .OrderBy(r => r.QuantityOnHand)
            .Take(10)
            .Select(r => new BranchInventoryDto
            {
                Id = r.Id,
                BranchId = r.BranchId,
                ProductId = r.ProductId,
                ProductName = r.Name,
                ProductSKU = r.SKU,
                ProductUnit = r.Unit,
                ProductIsActive = true,
                QuantityOnHand = r.QuantityOnHand,
                MinimumStock = r.MinimumStock,
                MaximumStock = r.MaximumStock,
                LastRestockedDate = r.LastRestockedDate,
                LastSoldDate = r.LastSoldDate,
                ConcurrencyStamp = r.ConcurrencyStamp
            }).ToList();

        // Movement summary (last 30 days)
        var thirtyDaysAgo = Clock.Now.AddDays(-30);
        var recentMovementsQ = movementsQ.Where(m => m.CreationTime >= thirtyDaysAgo);
        var branchesForMovements = await _branchRepository.GetQueryableAsync();
        var productsForMovements = await _productRepository.GetQueryableAsync();

        var movementRows = await AsyncExecuter.ToListAsync(
            from m in recentMovementsQ
            join b in branchesForMovements on m.BranchId equals b.Id
            join p in productsForMovements on m.ProductId equals p.Id
            orderby m.CreationTime descending
            select new
            {
                m.Id,
                m.CreationTime,
                m.BranchId,
                BranchName = b.Name,
                m.ProductId,
                ProductName = p.Name,
                ProductSKU = p.SKU,
                ProductUnit = p.Unit,
                m.MovementType,
                m.Quantity,
                m.QuantityBefore,
                m.QuantityAfter,
                m.ReferenceId,
                m.ReferenceType,
                m.Notes
            });

        var movementSummary = new StockMovementSummaryDto
        {
            TotalCount = movementRows.Count,
            PurchaseCount = movementRows.Count(r => r.MovementType == StockMovementTypes.Purchase),
            SaleCount = movementRows.Count(r => r.MovementType == StockMovementTypes.Sale),
            TransferInCount = movementRows.Count(r => r.MovementType == StockMovementTypes.TransferIn),
            TransferOutCount = movementRows.Count(r => r.MovementType == StockMovementTypes.TransferOut),
            ManualAdjustmentCount = movementRows.Count(r => r.MovementType == StockMovementTypes.ManualAdjustment)
        };

        var recentMovements = movementRows.Take(8).Select(r => new StockMovementDto
        {
            Id = r.Id,
            CreationTime = r.CreationTime,
            BranchId = r.BranchId,
            BranchName = r.BranchName,
            ProductId = r.ProductId,
            ProductName = r.ProductName,
            ProductSKU = r.ProductSKU,
            ProductUnit = r.ProductUnit,
            MovementType = r.MovementType,
            Quantity = r.Quantity,
            QuantityBefore = r.QuantityBefore,
            QuantityAfter = r.QuantityAfter,
            ReferenceId = r.ReferenceId,
            ReferenceType = r.ReferenceType,
            Notes = r.Notes
        }).ToList();

        return new InventoryDashboardDto
        {
            TotalProducts = totalProducts,
            ActiveProducts = activeProducts,
            TotalBranches = allBranches.Count,
            ActiveBranches = allBranches.Count(b => b.IsActive),
            TotalCategories = totalCategories,
            TotalSuppliers = totalSuppliers,
            TotalInventoryItems = totalItems,
            HealthyStockCount = healthyStock.Count,
            LowStockCount = lowStock.Count,
            OutOfStockCount = outOfStock.Count,
            MovementSummary = movementSummary,
            BranchStats = branchStats,
            RecentMovements = recentMovements,
            CriticalStockItems = criticalRows
        };
    }

    private async Task<List<Guid>?> GetAccessibleBranchIdsAsync()
    {
        if (await AuthorizationService.IsGrantedAsync(InventoryPermissions.BranchInventory.ManageAll))
            return null;

        var userId = CurrentUser.Id;
        if (userId == null) return new List<Guid>();

        var mine = await _branchRepository.GetListAsync(b => b.IsActive && b.ManagerUserId == userId);
        return mine.Select(b => b.Id).ToList();
    }
}
