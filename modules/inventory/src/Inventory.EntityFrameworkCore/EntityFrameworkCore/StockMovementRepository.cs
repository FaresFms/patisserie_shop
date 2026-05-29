using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.StockMovements;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Inventory.EntityFrameworkCore;

public class StockMovementRepository
    : EfCoreRepository<InventoryDbContext, AppStockMovement, Guid>,
      IStockMovementRepository
{
    public StockMovementRepository(IDbContextProvider<InventoryDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<StockMovementWithContext>> GetRecentWithContextAsync(
        IReadOnlyCollection<Guid>? branchIdScope,
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        var query =
            from m in dbContext.Set<AppStockMovement>()
            join b in dbContext.Set<AppBranch>() on m.BranchId equals b.Id
            join p in dbContext.Set<AppProduct>() on m.ProductId equals p.Id
            where m.CreationTime >= since
            orderby m.CreationTime descending
            select new StockMovementWithContext { Movement = m, Branch = b, Product = p };

        if (branchIdScope != null)
        {
            query = query.Where(x => branchIdScope.Contains(x.Movement.BranchId));
        }

        return await query.ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<long> CountFilteredAsync(
        StockMovementListFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<StockMovementWithContext>> GetFilteredListAsync(
        StockMovementListFilter filter,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter);

        var ordered = query
            .OrderBy(ResolveSorting(sorting))
            .Skip(skipCount)
            .Take(maxResultCount);

        return await ordered.ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<StockMovementTypeCount>> GetCountsByTypeAsync(
        StockMovementListFilter filter,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var movements = ApplyScalarFilters(dbContext.Set<AppStockMovement>().AsQueryable(), filter);

        if (!string.IsNullOrWhiteSpace(filter.Filter))
        {
            var f = filter.Filter.Trim().ToLower();
            var matchingIds =
                from m in movements
                join p in dbContext.Set<AppProduct>() on m.ProductId equals p.Id
                where p.Name.ToLower().Contains(f) || p.SKU.ToLower().Contains(f) ||
                      (m.Notes != null && m.Notes.ToLower().Contains(f))
                select m.Id;
            movements = movements.Where(m => matchingIds.Contains(m.Id));
        }

        var grouped = movements
            .GroupBy(m => m.MovementType)
            .Select(g => new StockMovementTypeCount { MovementType = g.Key, Count = g.Count() });

        return await grouped.ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<StockMovementWithContext?> GetWithContextAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        var result = await (
            from m in dbContext.Set<AppStockMovement>()
            join b in dbContext.Set<AppBranch>() on m.BranchId equals b.Id
            join p in dbContext.Set<AppProduct>() on m.ProductId equals p.Id
            where m.Id == id
            select new StockMovementWithContext { Movement = m, Branch = b, Product = p }
        ).FirstOrDefaultAsync(GetCancellationToken(cancellationToken));

        return result;
    }

    private static IQueryable<AppStockMovement> ApplyScalarFilters(
        IQueryable<AppStockMovement> movements,
        StockMovementListFilter filter)
    {
        if (filter.BranchIdScope != null)
        {
            var scope = filter.BranchIdScope;
            movements = movements.Where(m => scope.Contains(m.BranchId));
        }
        if (filter.BranchId.HasValue)
        {
            var branchId = filter.BranchId.Value;
            movements = movements.Where(m => m.BranchId == branchId);
        }
        if (filter.ProductId.HasValue)
        {
            var productId = filter.ProductId.Value;
            movements = movements.Where(m => m.ProductId == productId);
        }
        if (!string.IsNullOrWhiteSpace(filter.MovementType) && StockMovementTypes.IsValid(filter.MovementType))
        {
            var type = filter.MovementType;
            movements = movements.Where(m => m.MovementType == type);
        }
        if (filter.FromDate.HasValue)
        {
            var from = filter.FromDate.Value;
            movements = movements.Where(m => m.CreationTime >= from);
        }
        if (filter.ToDate.HasValue)
        {
            var to = filter.ToDate.Value;
            movements = movements.Where(m => m.CreationTime <= to);
        }
        return movements;
    }

    private async Task<IQueryable<StockMovementWithContext>> BuildFilteredQueryAsync(StockMovementListFilter filter)
    {
        var dbContext = await GetDbContextAsync();

        var movements = dbContext.Set<AppStockMovement>().AsQueryable();

        if (filter.BranchIdScope != null)
        {
            var scope = filter.BranchIdScope;
            movements = movements.Where(m => scope.Contains(m.BranchId));
        }
        if (filter.BranchId.HasValue)
        {
            var branchId = filter.BranchId.Value;
            movements = movements.Where(m => m.BranchId == branchId);
        }
        if (filter.ProductId.HasValue)
        {
            var productId = filter.ProductId.Value;
            movements = movements.Where(m => m.ProductId == productId);
        }
        if (!string.IsNullOrWhiteSpace(filter.MovementType) && StockMovementTypes.IsValid(filter.MovementType))
        {
            var type = filter.MovementType;
            movements = movements.Where(m => m.MovementType == type);
        }
        if (filter.FromDate.HasValue)
        {
            var from = filter.FromDate.Value;
            movements = movements.Where(m => m.CreationTime >= from);
        }
        if (filter.ToDate.HasValue)
        {
            var to = filter.ToDate.Value;
            movements = movements.Where(m => m.CreationTime <= to);
        }

        var joined =
            from m in movements
            join b in dbContext.Set<AppBranch>() on m.BranchId equals b.Id
            join p in dbContext.Set<AppProduct>() on m.ProductId equals p.Id
            select new StockMovementWithContext { Movement = m, Branch = b, Product = p };

        if (!string.IsNullOrWhiteSpace(filter.Filter))
        {
            var f = filter.Filter.Trim().ToLower();
            joined = joined.Where(x =>
                x.Product.Name.ToLower().Contains(f) ||
                x.Product.SKU.ToLower().Contains(f) ||
                (x.Movement.Notes != null && x.Movement.Notes.ToLower().Contains(f)));
        }

        return joined;
    }

    private static string ResolveSorting(string? sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
            return $"{nameof(StockMovementWithContext.Movement)}.{nameof(AppStockMovement.CreationTime)} desc";

        var s = sorting.Trim();
        if (s.StartsWith("BranchName", StringComparison.OrdinalIgnoreCase))
            return s.Replace("BranchName", $"{nameof(StockMovementWithContext.Branch)}.{nameof(AppBranch.Name)}", StringComparison.OrdinalIgnoreCase);
        if (s.StartsWith("ProductName", StringComparison.OrdinalIgnoreCase))
            return s.Replace("ProductName", $"{nameof(StockMovementWithContext.Product)}.{nameof(AppProduct.Name)}", StringComparison.OrdinalIgnoreCase);
        if (s.StartsWith("ProductSKU", StringComparison.OrdinalIgnoreCase))
            return s.Replace("ProductSKU", $"{nameof(StockMovementWithContext.Product)}.{nameof(AppProduct.SKU)}", StringComparison.OrdinalIgnoreCase);
        return $"{nameof(StockMovementWithContext.Movement)}.{s}";
    }
}
