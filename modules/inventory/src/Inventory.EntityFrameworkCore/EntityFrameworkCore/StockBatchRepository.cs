using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.StockBatches;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Inventory.EntityFrameworkCore;

public class StockBatchRepository
    : EfCoreRepository<InventoryDbContext, AppStockBatch, Guid>,
      IStockBatchRepository
{
    public StockBatchRepository(IDbContextProvider<InventoryDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<AppStockBatch>> GetOpenBatchesAsync(
        Guid branchId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        return await dbContext.Set<AppStockBatch>()
            .Where(b => b.BranchId == branchId && b.ProductId == productId && b.QuantityRemaining > 0)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<StockBatchWithProduct>> GetExpiringWithProductAsync(
        DateTime maxExpiryDate,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        return await (
            from batch in dbContext.Set<AppStockBatch>()
            join p in dbContext.Set<AppProduct>() on batch.ProductId equals p.Id
            where batch.QuantityRemaining > 0 && batch.ExpiryDate <= maxExpiryDate && p.IsActive
            orderby batch.ExpiryDate
            select new StockBatchWithProduct { Batch = batch, Product = p }
        ).ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<StockBatchWithDetails>> GetExpiringInWindowAsync(
        DateTime fromDate,
        DateTime toDate,
        IReadOnlyCollection<Guid>? branchIdScope,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        var fromDay = fromDate.Date;
        var toDay = toDate.Date;

        var query =
            from batch in dbContext.Set<AppStockBatch>()
            join p in dbContext.Set<AppProduct>() on batch.ProductId equals p.Id
            join br in dbContext.Set<AppBranch>() on batch.BranchId equals br.Id
            where batch.QuantityRemaining > 0
                  && p.IsActive
                  && batch.ExpiryDate >= fromDay
                  && batch.ExpiryDate <= toDay
            select new StockBatchWithDetails { Batch = batch, Product = p, Branch = br };

        if (branchIdScope != null)
        {
            query = query.Where(x => branchIdScope.Contains(x.Batch.BranchId));
        }

        return await query
            .OrderBy(x => x.Batch.ExpiryDate)
            .ThenBy(x => x.Branch.Name)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<int> GetExpiredQuantityAsync(
        Guid branchId,
        Guid productId,
        DateTime todayUtc,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var today = todayUtc.Date;

        // ExpiryDate is stored date-precision; "expired" means strictly before today
        // (the expiry day itself still counts as sellable, see AppStockBatch.IsExpired).
        return await dbContext.Set<AppStockBatch>()
            .Where(b => b.BranchId == branchId
                        && b.ProductId == productId
                        && b.QuantityRemaining > 0
                        && b.ExpiryDate < today)
            .SumAsync(b => b.QuantityRemaining, GetCancellationToken(cancellationToken));
    }

    public async Task<long> CountWithDetailsAsync(
        string? filter,
        Guid? branchId,
        bool includeDepleted,
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildJoinedQueryAsync(filter, branchId, includeDepleted, branchIdScope);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<StockBatchWithDetails>> GetListWithDetailsAsync(
        string? filter,
        Guid? branchId,
        bool includeDepleted,
        IReadOnlyCollection<Guid>? branchIdScope,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildJoinedQueryAsync(filter, branchId, includeDepleted, branchIdScope);

        var ordered = query
            .OrderBy(ResolveSorting(sorting))
            .Skip(skipCount)
            .Take(maxResultCount);

        return await ordered.ToListAsync(GetCancellationToken(cancellationToken));
    }

    private static string ResolveSorting(string? sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
        {
            return $"{nameof(StockBatchWithDetails.Batch)}.{nameof(AppStockBatch.ExpiryDate)}";
        }

        var s = sorting.Trim();
        if (s.StartsWith("ProductName", StringComparison.OrdinalIgnoreCase))
            return s.Replace("ProductName", $"{nameof(StockBatchWithDetails.Product)}.{nameof(AppProduct.Name)}", StringComparison.OrdinalIgnoreCase);
        if (s.StartsWith("ProductSKU", StringComparison.OrdinalIgnoreCase))
            return s.Replace("ProductSKU", $"{nameof(StockBatchWithDetails.Product)}.{nameof(AppProduct.SKU)}", StringComparison.OrdinalIgnoreCase);
        if (s.StartsWith("BranchName", StringComparison.OrdinalIgnoreCase))
            return s.Replace("BranchName", $"{nameof(StockBatchWithDetails.Branch)}.{nameof(AppBranch.Name)}", StringComparison.OrdinalIgnoreCase);
        return $"{nameof(StockBatchWithDetails.Batch)}.{s}";
    }

    private async Task<IQueryable<StockBatchWithDetails>> BuildJoinedQueryAsync(
        string? filter,
        Guid? branchId,
        bool includeDepleted,
        IReadOnlyCollection<Guid>? branchIdScope)
    {
        var dbContext = await GetDbContextAsync();

        var query =
            from batch in dbContext.Set<AppStockBatch>()
            join p in dbContext.Set<AppProduct>() on batch.ProductId equals p.Id
            join br in dbContext.Set<AppBranch>() on batch.BranchId equals br.Id
            select new StockBatchWithDetails { Batch = batch, Product = p, Branch = br };

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(x =>
                x.Product.Name.ToLower().Contains(f) ||
                x.Product.SKU.ToLower().Contains(f) ||
                x.Batch.BatchNumber.ToLower().Contains(f));
        }

        if (branchId.HasValue)
        {
            query = query.Where(x => x.Batch.BranchId == branchId.Value);
        }

        if (branchIdScope != null)
        {
            query = query.Where(x => branchIdScope.Contains(x.Batch.BranchId));
        }

        if (!includeDepleted)
        {
            query = query.Where(x => x.Batch.QuantityRemaining > 0);
        }

        return query;
    }
}
