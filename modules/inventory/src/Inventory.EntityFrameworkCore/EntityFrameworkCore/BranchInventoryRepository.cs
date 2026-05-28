using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Inventory.BranchInventory;
using Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Inventory.EntityFrameworkCore;

public class BranchInventoryRepository
    : EfCoreRepository<InventoryDbContext, AppBranchInventory, Guid>,
      IBranchInventoryRepository
{
    public BranchInventoryRepository(IDbContextProvider<InventoryDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<long> CountWithProductAsync(
        Guid branchId,
        string? filter,
        bool onlyOutOfStock,
        bool onlyLowStock,
        bool includeInactiveProducts,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildJoinedQueryAsync(
            branchId, filter, onlyOutOfStock, onlyLowStock, includeInactiveProducts);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<BranchInventoryWithProduct>> GetListWithProductAsync(
        Guid branchId,
        string? filter,
        bool onlyOutOfStock,
        bool onlyLowStock,
        bool includeInactiveProducts,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildJoinedQueryAsync(
            branchId, filter, onlyOutOfStock, onlyLowStock, includeInactiveProducts);

        var ordered = query
            .OrderBy(ResolveSorting(sorting))
            .Skip(skipCount)
            .Take(maxResultCount);

        return await ordered.ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<BranchInventoryWithProduct> GetWithProductAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        var row = await (
            from inv in dbContext.Set<AppBranchInventory>()
            join p in dbContext.Set<AppProduct>() on inv.ProductId equals p.Id
            where inv.Id == id
            select new BranchInventoryWithProduct { Inventory = inv, Product = p }
        ).FirstOrDefaultAsync(GetCancellationToken(cancellationToken));

        if (row == null)
        {
            throw new EntityNotFoundException(typeof(AppBranchInventory), id);
        }
        return row;
    }

    public async Task<List<StockSnapshot>> GetActiveStockSnapshotsAsync(
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        return await (
            from inv in dbContext.Set<AppBranchInventory>()
            join p in dbContext.Set<AppProduct>() on inv.ProductId equals p.Id
            where inv.BranchId == branchId && p.IsActive
            select new StockSnapshot
            {
                QuantityOnHand = inv.QuantityOnHand,
                MinimumStock = inv.MinimumStock
            }
        ).ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<InventoryStockRow>> GetActiveStockRowsAsync(
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        var query =
            from inv in dbContext.Set<AppBranchInventory>()
            join p in dbContext.Set<AppProduct>() on inv.ProductId equals p.Id
            where p.IsActive
            select new InventoryStockRow { Inventory = inv, Product = p };

        if (branchIdScope != null)
        {
            query = query.Where(x => branchIdScope.Contains(x.Inventory.BranchId));
        }

        return await query.ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<InventoryStockRow>> GetAvailableProductsAsync(
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        return await (
            from inv in dbContext.Set<AppBranchInventory>()
            join p in dbContext.Set<AppProduct>() on inv.ProductId equals p.Id
            where inv.BranchId == branchId && inv.QuantityOnHand > 0 && p.IsActive
            orderby p.Name
            select new InventoryStockRow { Inventory = inv, Product = p }
        ).ToListAsync(GetCancellationToken(cancellationToken));
    }

    private static string ResolveSorting(string? sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting)) return $"{nameof(BranchInventoryWithProduct.Product)}.{nameof(AppProduct.Name)}";
        var s = sorting.Trim();
        if (s.StartsWith("ProductName", StringComparison.OrdinalIgnoreCase))
            return s.Replace("ProductName", $"{nameof(BranchInventoryWithProduct.Product)}.{nameof(AppProduct.Name)}", StringComparison.OrdinalIgnoreCase);
        if (s.StartsWith("ProductSKU", StringComparison.OrdinalIgnoreCase))
            return s.Replace("ProductSKU", $"{nameof(BranchInventoryWithProduct.Product)}.{nameof(AppProduct.SKU)}", StringComparison.OrdinalIgnoreCase);
        if (s.StartsWith("ProductUnit", StringComparison.OrdinalIgnoreCase))
            return s.Replace("ProductUnit", $"{nameof(BranchInventoryWithProduct.Product)}.{nameof(AppProduct.Unit)}", StringComparison.OrdinalIgnoreCase);
        return $"{nameof(BranchInventoryWithProduct.Inventory)}.{s}";
    }

    private async Task<IQueryable<BranchInventoryWithProduct>> BuildJoinedQueryAsync(
        Guid branchId,
        string? filter,
        bool onlyOutOfStock,
        bool onlyLowStock,
        bool includeInactiveProducts)
    {
        var dbContext = await GetDbContextAsync();

        var query =
            from inv in dbContext.Set<AppBranchInventory>()
            join p in dbContext.Set<AppProduct>() on inv.ProductId equals p.Id
            where inv.BranchId == branchId
            select new BranchInventoryWithProduct { Inventory = inv, Product = p };

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(x =>
                x.Product.Name.ToLower().Contains(f) ||
                x.Product.SKU.ToLower().Contains(f));
        }

        if (onlyOutOfStock)
        {
            query = query.Where(x => x.Inventory.QuantityOnHand <= 0);
        }
        else if (onlyLowStock)
        {
            query = query.Where(x => x.Inventory.QuantityOnHand <= x.Inventory.MinimumStock);
        }

        if (!includeInactiveProducts)
        {
            query = query.Where(x => x.Product.IsActive);
        }

        return query;
    }
}
