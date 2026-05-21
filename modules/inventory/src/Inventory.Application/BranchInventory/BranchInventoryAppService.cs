using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
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
    private readonly IRepository<AppBranchInventory, Guid> _inventoryRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly BranchInventoryManager _manager;

    public BranchInventoryAppService(
        IRepository<AppBranchInventory, Guid> inventoryRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository,
        BranchInventoryManager manager)
    {
        _inventoryRepository = inventoryRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _manager = manager;
    }

    public async Task<BranchInventoryDto> GetAsync(Guid id)
    {
        var inv = await _inventoryRepository.GetAsync(id);
        await EnsureBranchAccessAsync(inv.BranchId);
        return await ProjectAsync(inv);
    }

    public async Task<PagedResultDto<BranchInventoryDto>> GetListAsync(GetBranchInventoryInput input)
    {
        if (input.BranchId == Guid.Empty)
        {
            throw new BusinessException("Inventory:BranchInventory:BranchIdRequired");
        }
        await EnsureBranchAccessAsync(input.BranchId);

        var invQ = await _inventoryRepository.GetQueryableAsync();
        var prodQ = await _productRepository.GetQueryableAsync();

        var query = from inv in invQ
                    join p in prodQ on inv.ProductId equals p.Id
                    where inv.BranchId == input.BranchId
                    select new { inv, p };

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.Trim().ToLower();
            query = query.Where(x => x.p.Name.ToLower().Contains(f) || x.p.SKU.ToLower().Contains(f));
        }

        if (input.OnlyOutOfStock == true)
        {
            query = query.Where(x => x.inv.QuantityOnHand <= 0);
        }
        else if (input.OnlyLowStock == true)
        {
            query = query.Where(x => x.inv.QuantityOnHand <= x.inv.MinimumStock);
        }

        if (input.IncludeInactiveProducts != true)
        {
            query = query.Where(x => x.p.IsActive);
        }

        var totalCount = await AsyncExecuter.CountAsync(query);

        var sorting = ResolveSorting(input.Sorting);
        var ordered = query.OrderBy(sorting).Skip(input.SkipCount).Take(input.MaxResultCount);

        var rows = await AsyncExecuter.ToListAsync(ordered);

        var items = rows.Select(r => Project(r.inv, r.p)).ToList();
        return new PagedResultDto<BranchInventoryDto>(totalCount, items);
    }

    public async Task<BranchInventoryStatsDto> GetStatsAsync(Guid branchId)
    {
        await EnsureBranchAccessAsync(branchId);
        var q = await _inventoryRepository.GetQueryableAsync();
        var pq = await _productRepository.GetQueryableAsync();

        var rows = await AsyncExecuter.ToListAsync(
            from inv in q
            join p in pq on inv.ProductId equals p.Id
            where inv.BranchId == branchId && p.IsActive
            select new { inv.QuantityOnHand, inv.MinimumStock });

        var stats = new BranchInventoryStatsDto
        {
            TotalItems = rows.Count,
            OutOfStockCount = rows.Count(r => r.QuantityOnHand <= 0),
            LowStockCount = rows.Count(r => r.QuantityOnHand > 0 && r.QuantityOnHand <= r.MinimumStock),
        };
        stats.HealthyStockCount = stats.TotalItems - stats.LowStockCount - stats.OutOfStockCount;
        return stats;
    }

    public async Task<List<Guid>> GetAccessibleBranchIdsAsync()
    {
        if (await IsManageAllAsync())
        {
            var all = await _branchRepository.GetListAsync(b => b.IsActive);
            return all.Select(b => b.Id).ToList();
        }

        var userId = CurrentUser.Id;
        if (userId == null) return new List<Guid>();

        var mine = await _branchRepository.GetListAsync(b => b.IsActive && b.ManagerUserId == userId);
        return mine.Select(b => b.Id).ToList();
    }

    [Authorize(InventoryPermissions.BranchInventory.Initialize)]
    public async Task<BranchInventoryDto> InitializeAsync(InitializeBranchInventoryDto input)
    {
        await EnsureBranchAccessAsync(input.BranchId);
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
        await EnsureBranchAccessAsync(inv.BranchId);

        if (!string.IsNullOrEmpty(input.ConcurrencyStamp) &&
            !string.Equals(inv.ConcurrencyStamp, input.ConcurrencyStamp, StringComparison.Ordinal))
        {
            throw new BusinessException("Inventory:BranchInventory:Concurrency");
        }

        await _manager.AdjustStockAsync(inv, input.NewQuantity, input.MovementType, input.Notes);
        await _inventoryRepository.UpdateAsync(inv, autoSave: true);
        return await ProjectAsync(inv);
    }

    [Authorize(InventoryPermissions.BranchInventory.SetLimits)]
    public async Task<BranchInventoryDto> UpdateLimitsAsync(Guid id, UpdateStockLimitsDto input)
    {
        var inv = await _inventoryRepository.GetAsync(id);
        await EnsureBranchAccessAsync(inv.BranchId);

        _manager.UpdateLimits(inv, input.MinimumStock, input.MaximumStock);
        await _inventoryRepository.UpdateAsync(inv, autoSave: true);
        return await ProjectAsync(inv);
    }

    [Authorize(InventoryPermissions.BranchInventory.ManageAll)]
    public async Task DeleteAsync(Guid id)
    {
        await _inventoryRepository.DeleteAsync(id);
    }

    private async Task<bool> IsManageAllAsync()
    {
        return await AuthorizationService.IsGrantedAsync(InventoryPermissions.BranchInventory.ManageAll);
    }

    private async Task EnsureBranchAccessAsync(Guid branchId)
    {
        if (await IsManageAllAsync()) return;

        var userId = CurrentUser.Id;
        if (userId == null ||
            !await _branchRepository.AnyAsync(b => b.Id == branchId && b.ManagerUserId == userId))
        {
            throw new BusinessException(InventoryErrorCodes.BranchAccessDenied)
                .WithData("BranchId", branchId);
        }
    }

    private async Task<BranchInventoryDto> ProjectAsync(AppBranchInventory inv)
    {
        var product = await _productRepository.GetAsync(inv.ProductId);
        return Project(inv, product);
    }

    private static BranchInventoryDto Project(AppBranchInventory inv, AppProduct product)
    {
        return new BranchInventoryDto
        {
            Id = inv.Id,
            BranchId = inv.BranchId,
            ProductId = inv.ProductId,
            ProductName = product.Name,
            ProductSKU = product.SKU,
            ProductUnit = product.Unit,
            ProductIsActive = product.IsActive,
            QuantityOnHand = inv.QuantityOnHand,
            MinimumStock = inv.MinimumStock,
            MaximumStock = inv.MaximumStock,
            LastRestockedDate = inv.LastRestockedDate,
            LastSoldDate = inv.LastSoldDate,
            ConcurrencyStamp = inv.ConcurrencyStamp
        };
    }

    private static string ResolveSorting(string? sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting)) return "p.Name";
        var s = sorting.Trim();
        if (s.StartsWith("ProductName", StringComparison.OrdinalIgnoreCase))
            return s.Replace("ProductName", "p.Name", StringComparison.OrdinalIgnoreCase);
        if (s.StartsWith("ProductSKU", StringComparison.OrdinalIgnoreCase))
            return s.Replace("ProductSKU", "p.SKU", StringComparison.OrdinalIgnoreCase);
        if (s.StartsWith("ProductUnit", StringComparison.OrdinalIgnoreCase))
            return s.Replace("ProductUnit", "p.Unit", StringComparison.OrdinalIgnoreCase);
        // Default: inventory columns
        return $"inv.{s}";
    }
}
