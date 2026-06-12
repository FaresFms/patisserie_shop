using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Products;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Inventory.EntityFrameworkCore;

public class ProductRepository
    : EfCoreRepository<InventoryDbContext, AppProduct, Guid>,
      IProductRepository
{
    public ProductRepository(IDbContextProvider<InventoryDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<long> CountFilteredAsync(
        string? filter,
        Guid? categoryId,
        Guid? defaultSupplierId,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, categoryId, defaultSupplierId, isActive);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<AppProduct>> GetFilteredListAsync(
        string? filter,
        Guid? categoryId,
        Guid? defaultSupplierId,
        bool? isActive,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, categoryId, defaultSupplierId, isActive);

        var ordered = query
            .OrderBy(ResolveSorting(sorting))
            .Skip(skipCount)
            .Take(maxResultCount);

        return await ordered.ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<AppProduct>> GetActiveLookupAsync(
        string? filter,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        query = query.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(f) || p.SKU.ToLower().Contains(f));
        }

        return await query.OrderBy(p => p.Name).Take(maxResults)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    private async Task<IQueryable<AppProduct>> BuildFilteredQueryAsync(
        string? filter,
        Guid? categoryId,
        Guid? defaultSupplierId,
        bool? isActive)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(f) ||
                p.SKU.ToLower().Contains(f));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (defaultSupplierId.HasValue)
        {
            query = query.Where(p => p.DefaultSupplierId == defaultSupplierId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        return query;
    }

    private static string ResolveSorting(string? sorting)
        => string.IsNullOrWhiteSpace(sorting) ? nameof(AppProduct.Name) : sorting.Trim();
}
