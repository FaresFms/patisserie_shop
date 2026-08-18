using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Branches;
using Inventory.Entities;
using Inventory.Localization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Inventory.EntityFrameworkCore;

public class BranchRepository
    : EfCoreRepository<InventoryDbContext, AppBranch, Guid>,
      IBranchRepository
{
    public BranchRepository(IDbContextProvider<InventoryDbContext> dbContextProvider)
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

    public async Task<List<AppBranch>> GetFilteredListAsync(
        string? filter,
        bool? isActive,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, isActive);
        return await query
            .OrderBy(ResolveSorting(sorting))
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<AppBranch>> GetActiveLookupAsync(
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        var query = (await GetQueryableAsync()).Where(b => b.IsActive);
        query = LocalizedBusinessText.IsArabic
            ? query.OrderBy(b => b.NameAr)
            : query.OrderBy(b => b.NameEn);

        return await query.Take(maxResults)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    private async Task<IQueryable<AppBranch>> BuildFilteredQueryAsync(string? filter, bool? isActive)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var value = filter.Trim().ToLower();
            query = query.Where(b =>
                b.NameAr.ToLower().Contains(value) ||
                b.NameEn.ToLower().Contains(value) ||
                (b.AddressAr != null && b.AddressAr.ToLower().Contains(value)) ||
                (b.AddressEn != null && b.AddressEn.ToLower().Contains(value)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(b => b.IsActive == isActive.Value);
        }

        return query;
    }

    private static string ResolveSorting(string? sorting)
    {
        var localizedName = LocalizedBusinessText.IsArabic
            ? nameof(AppBranch.NameAr)
            : nameof(AppBranch.NameEn);
        var localizedAddress = LocalizedBusinessText.IsArabic
            ? nameof(AppBranch.AddressAr)
            : nameof(AppBranch.AddressEn);

        if (string.IsNullOrWhiteSpace(sorting))
        {
            return localizedName;
        }

        var value = sorting.Trim();
        if (value.StartsWith("Name", StringComparison.OrdinalIgnoreCase))
        {
            return value.Replace("Name", localizedName, StringComparison.OrdinalIgnoreCase);
        }

        if (value.StartsWith("Address", StringComparison.OrdinalIgnoreCase))
        {
            return value.Replace("Address", localizedAddress, StringComparison.OrdinalIgnoreCase);
        }

        return value;
    }
}
