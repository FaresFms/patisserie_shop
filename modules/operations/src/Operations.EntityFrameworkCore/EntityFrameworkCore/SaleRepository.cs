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

    public async Task<List<DailySaleAggregate>> GetDailySalesAsync(
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive,
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        query = query.Where(s => s.SaleDate >= fromUtcInclusive && s.SaleDate < toUtcExclusive);
        if (branchIdScope != null)
        {
            query = query.Where(s => branchIdScope.Contains(s.BranchId));
        }

        var grouped = query
            .GroupBy(s => s.SaleDate.Date)
            .Select(g => new DailySaleAggregate
            {
                Date = g.Key,
                TotalAmount = g.Sum(s => s.TotalAmount),
                SaleCount = g.Count()
            });

        return await grouped.ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<ProductSalesAggregate>> GetTopProductsAsync(
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive,
        IReadOnlyCollection<Guid>? branchIdScope,
        int take,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        var sales = dbContext.Set<AppSale>()
            .Where(s => s.SaleDate >= fromUtcInclusive && s.SaleDate < toUtcExclusive);

        if (branchIdScope != null)
        {
            sales = sales.Where(s => branchIdScope.Contains(s.BranchId));
        }

        var lines = from s in sales
                    from i in s.Items
                    select new { i.ProductId, i.Quantity, i.Subtotal };

        var grouped = lines
            .GroupBy(x => x.ProductId)
            .Select(g => new ProductSalesAggregate
            {
                ProductId = g.Key,
                TotalQuantitySold = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.Subtotal)
            })
            .OrderByDescending(p => p.TotalQuantitySold)
            .Take(take);

        return await grouped.ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<BranchSalesAggregate>> GetSalesByBranchAsync(
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive,
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        query = query.Where(s => s.SaleDate >= fromUtcInclusive && s.SaleDate < toUtcExclusive);
        if (branchIdScope != null)
        {
            query = query.Where(s => branchIdScope.Contains(s.BranchId));
        }

        var grouped = query
            .GroupBy(s => s.BranchId)
            .Select(g => new BranchSalesAggregate
            {
                BranchId = g.Key,
                TotalAmount = g.Sum(s => s.TotalAmount),
                SaleCount = g.Count()
            });

        return await grouped.ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<ProductBranchSalesAggregate>> GetProductBranchSalesAggregatesAsync(
        DateTime from7Utc,
        DateTime from30Utc,
        DateTime toUtcExclusive,
        CancellationToken ct = default)
    {
        var dbContext = await GetDbContextAsync();

        var sales = dbContext.Set<AppSale>()
            .Where(s => s.SaleDate >= from30Utc && s.SaleDate < toUtcExclusive);

        var lines = from s in sales
                    from i in s.Items
                    select new { s.BranchId, s.SaleDate, i.ProductId, i.Quantity, i.UnitPrice };

        var grouped = lines
            .GroupBy(x => new { x.ProductId, x.BranchId })
            .Select(g => new ProductBranchSalesAggregate
            {
                ProductId = g.Key.ProductId,
                BranchId = g.Key.BranchId,
                QuantitySold7 = g.Sum(x => x.SaleDate >= from7Utc ? x.Quantity : 0),
                QuantitySold30 = g.Sum(x => x.Quantity),
                Revenue30 = g.Sum(x => x.Quantity * x.UnitPrice)
            });

        return await grouped.ToListAsync(GetCancellationToken(ct));
    }
}
