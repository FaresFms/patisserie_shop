using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Operations.Entities;
using Volo.Abp.Domain.Repositories;

namespace Operations.Sales;

/// <summary>
/// Custom repository for <see cref="AppSale"/>. Owns every persistence-level
/// concern the app service must not contain: multi-parameter filtering,
/// sort-key translation, paging and the item-count aggregation.
/// </summary>
public interface ISaleRepository : IRepository<AppSale, Guid>
{
    /// <summary>Loads a sale together with its line items, or throws if missing.</summary>
    Task<AppSale> GetWithItemsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<long> CountFilteredAsync(
        IReadOnlyCollection<Guid>? branchIdScope,
        Guid? branchId,
        DateTime? fromDate,
        DateTime? toDate,
        string? filter,
        CancellationToken cancellationToken = default);

    Task<List<SaleListRow>> GetFilteredListAsync(
        IReadOnlyCollection<Guid>? branchIdScope,
        Guid? branchId,
        DateTime? fromDate,
        DateTime? toDate,
        string? filter,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    /// <summary>Sum + count grouped by sale date (UTC date component) in the supplied window.</summary>
    Task<List<DailySaleAggregate>> GetDailySalesAsync(
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive,
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default);

    /// <summary>Top selling products in the window by quantity sold, with revenue.</summary>
    Task<List<ProductSalesAggregate>> GetTopProductsAsync(
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive,
        IReadOnlyCollection<Guid>? branchIdScope,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Sum + count grouped by branch in the supplied window.</summary>
    Task<List<BranchSalesAggregate>> GetSalesByBranchAsync(
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive,
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default);
}
