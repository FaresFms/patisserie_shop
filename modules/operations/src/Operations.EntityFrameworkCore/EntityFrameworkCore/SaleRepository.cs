using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Operations.Entities;
using Operations.Sales;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Operations.EntityFrameworkCore;

public class SaleRepository
    : EfCoreRepository<OperationsDbContext, AppSale, Guid>,
      ISaleRepository
{
    public SaleRepository(IDbContextProvider<OperationsDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<AppSale> GetWithItemsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = await WithDetailsAsync(s => s.Items);
        var sale = await query
            .Where(s => s.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));

        if (sale == null)
        {
            throw new EntityNotFoundException(typeof(AppSale), id);
        }
        return sale;
    }

    public async Task<long> CountFilteredAsync(
        IReadOnlyCollection<Guid>? branchIdScope,
        Guid? branchId,
        DateTime? fromDate,
        DateTime? toDate,
        string? filter,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(branchIdScope, branchId, fromDate, toDate, filter);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<SaleListRow>> GetFilteredListAsync(
        IReadOnlyCollection<Guid>? branchIdScope,
        Guid? branchId,
        DateTime? fromDate,
        DateTime? toDate,
        string? filter,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(branchIdScope, branchId, fromDate, toDate, filter);

        var rows = query
            .OrderBy(ResolveSorting(sorting))
            .Skip(skipCount)
            .Take(maxResultCount)
            .Select(s => new SaleListRow { Sale = s, ItemCount = s.Items.Count });

        return await rows.ToListAsync(GetCancellationToken(cancellationToken));
    }

    private async Task<IQueryable<AppSale>> BuildFilteredQueryAsync(
        IReadOnlyCollection<Guid>? branchIdScope,
        Guid? branchId,
        DateTime? fromDate,
        DateTime? toDate,
        string? filter)
    {
        var query = await GetQueryableAsync();

        if (branchIdScope != null)
        {
            query = query.Where(s => branchIdScope.Contains(s.BranchId));
        }

        if (branchId.HasValue)
        {
            query = query.Where(s => s.BranchId == branchId.Value);
        }

        if (fromDate.HasValue)
        {
            var f = fromDate.Value;
            query = query.Where(s => s.SaleDate >= f);
        }

        if (toDate.HasValue)
        {
            var t = toDate.Value;
            query = query.Where(s => s.SaleDate <= t);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(s => s.InvoiceNumber.ToLower().Contains(f));
        }

        return query;
    }

    private static string ResolveSorting(string? sorting)
        => string.IsNullOrWhiteSpace(sorting)
            ? $"{nameof(AppSale.SaleDate)} desc"
            : sorting.Trim();
}
