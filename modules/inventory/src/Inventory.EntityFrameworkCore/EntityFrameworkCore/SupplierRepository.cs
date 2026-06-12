using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Suppliers;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Inventory.EntityFrameworkCore;

public class SupplierRepository
    : EfCoreRepository<InventoryDbContext, AppSupplier, Guid>,
      ISupplierRepository
{
    public SupplierRepository(IDbContextProvider<InventoryDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<long> CountFilteredAsync(
        string? filter,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, isActive);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<AppSupplier>> GetFilteredListAsync(
        string? filter,
        bool? isActive,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, isActive);

        var ordered = query
            .OrderBy(ResolveSorting(sorting))
            .Skip(skipCount)
            .Take(maxResultCount);

        return await ordered.ToListAsync(GetCancellationToken(cancellationToken));
    }

    private async Task<IQueryable<AppSupplier>> BuildFilteredQueryAsync(string? filter, bool? isActive)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(f));
        }

        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        return query;
    }

    private static string ResolveSorting(string? sorting)
        => string.IsNullOrWhiteSpace(sorting) ? nameof(AppSupplier.Name) : sorting.Trim();
}
