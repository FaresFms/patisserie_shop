using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Inventory.StockMovements;

[Authorize(InventoryPermissions.StockMovements.Default)]
public class StockMovementAppService : InventoryAppService, IStockMovementAppService
{
    private readonly IRepository<AppStockMovement, Guid> _movementRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;

    public StockMovementAppService(
        IRepository<AppStockMovement, Guid> movementRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository)
    {
        _movementRepository = movementRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
    }

    public async Task<PagedResultDto<StockMovementDto>> GetListAsync(GetStockMovementsInput input)
    {
        var query = await BuildQueryAsync(input);

        var totalCount = await AsyncExecuter.CountAsync(query);

        var sorting = ResolveSorting(input.Sorting);
        var rows = await AsyncExecuter.ToListAsync(
            query.OrderBy(sorting).Skip(input.SkipCount).Take(input.MaxResultCount));

        var items = rows.Select(r => new StockMovementDto
        {
            Id = r.m.Id,
            CreationTime = r.m.CreationTime,
            BranchId = r.m.BranchId,
            BranchName = r.b.Name,
            ProductId = r.m.ProductId,
            ProductName = r.p.Name,
            ProductSKU = r.p.SKU,
            ProductUnit = r.p.Unit,
            MovementType = r.m.MovementType,
            Quantity = r.m.Quantity,
            QuantityBefore = r.m.QuantityBefore,
            QuantityAfter = r.m.QuantityAfter,
            ReferenceId = r.m.ReferenceId,
            ReferenceType = r.m.ReferenceType,
            Notes = r.m.Notes
        }).ToList();

        return new PagedResultDto<StockMovementDto>(totalCount, items);
    }

    public async Task<StockMovementSummaryDto> GetSummaryAsync(GetStockMovementsInput input)
    {
        var query = await BuildQueryAsync(input);
        var byType = await AsyncExecuter.ToListAsync(
            query.GroupBy(x => x.m.MovementType).Select(g => new { Type = g.Key, Count = g.Count() }));

        var summary = new StockMovementSummaryDto
        {
            TotalCount = byType.Sum(x => x.Count),
            PurchaseCount = byType.FirstOrDefault(x => x.Type == StockMovementTypes.Purchase)?.Count ?? 0,
            SaleCount = byType.FirstOrDefault(x => x.Type == StockMovementTypes.Sale)?.Count ?? 0,
            TransferInCount = byType.FirstOrDefault(x => x.Type == StockMovementTypes.TransferIn)?.Count ?? 0,
            TransferOutCount = byType.FirstOrDefault(x => x.Type == StockMovementTypes.TransferOut)?.Count ?? 0,
            ManualAdjustmentCount = byType.FirstOrDefault(x => x.Type == StockMovementTypes.ManualAdjustment)?.Count ?? 0,
        };
        return summary;
    }

    private async Task<IQueryable<JoinedMovement>> BuildQueryAsync(GetStockMovementsInput input)
    {
        var movements = await _movementRepository.GetQueryableAsync();
        var branches = await _branchRepository.GetQueryableAsync();
        var products = await _productRepository.GetQueryableAsync();

        var accessible = await GetAccessibleBranchIdsAsync();
        if (accessible != null)
        {
            var set = accessible.ToHashSet();
            movements = movements.Where(m => set.Contains(m.BranchId));
        }

        if (input.BranchId.HasValue)
        {
            movements = movements.Where(m => m.BranchId == input.BranchId.Value);
        }
        if (input.ProductId.HasValue)
        {
            movements = movements.Where(m => m.ProductId == input.ProductId.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.MovementType) && StockMovementTypes.IsValid(input.MovementType))
        {
            var t = input.MovementType;
            movements = movements.Where(m => m.MovementType == t);
        }
        if (input.FromDate.HasValue)
        {
            var from = input.FromDate.Value;
            movements = movements.Where(m => m.CreationTime >= from);
        }
        if (input.ToDate.HasValue)
        {
            var to = input.ToDate.Value;
            movements = movements.Where(m => m.CreationTime <= to);
        }

        var joined = from m in movements
                     join b in branches on m.BranchId equals b.Id
                     join p in products on m.ProductId equals p.Id
                     select new JoinedMovement { m = m, b = b, p = p };

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.Trim().ToLower();
            joined = joined.Where(x =>
                x.p.Name.ToLower().Contains(f) ||
                x.p.SKU.ToLower().Contains(f) ||
                (x.m.Notes != null && x.m.Notes.ToLower().Contains(f)));
        }

        return joined;
    }

    private async Task<List<Guid>?> GetAccessibleBranchIdsAsync()
    {
        if (await AuthorizationService.IsGrantedAsync(InventoryPermissions.StockMovements.ViewAll))
        {
            return null; // null = no branch restriction
        }
        var userId = CurrentUser.Id;
        if (userId == null) return new List<Guid>();
        var mine = await _branchRepository.GetListAsync(b => b.ManagerUserId == userId);
        return mine.Select(b => b.Id).ToList();
    }

    private static string ResolveSorting(string? sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting)) return "m.CreationTime desc";
        var s = sorting.Trim();
        if (s.StartsWith("CreationTime", StringComparison.OrdinalIgnoreCase))
            return s.Replace("CreationTime", "m.CreationTime", StringComparison.OrdinalIgnoreCase);
        if (s.StartsWith("BranchName", StringComparison.OrdinalIgnoreCase))
            return s.Replace("BranchName", "b.Name", StringComparison.OrdinalIgnoreCase);
        if (s.StartsWith("ProductName", StringComparison.OrdinalIgnoreCase))
            return s.Replace("ProductName", "p.Name", StringComparison.OrdinalIgnoreCase);
        if (s.StartsWith("ProductSKU", StringComparison.OrdinalIgnoreCase))
            return s.Replace("ProductSKU", "p.SKU", StringComparison.OrdinalIgnoreCase);
        if (s.StartsWith("MovementType", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("Quantity", StringComparison.OrdinalIgnoreCase))
            return $"m.{s}";
        return $"m.{s}";
    }

    private class JoinedMovement
    {
        public AppStockMovement m { get; set; } = null!;
        public AppBranch b { get; set; } = null!;
        public AppProduct p { get; set; } = null!;
    }
}
