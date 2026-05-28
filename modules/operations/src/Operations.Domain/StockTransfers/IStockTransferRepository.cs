using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Operations.Entities;
using Volo.Abp.Domain.Repositories;

namespace Operations.StockTransfers;

/// <summary>
/// Custom repository for <see cref="AppStockTransfer"/>. Owns every persistence-level
/// concern the app service must not contain: multi-parameter filtering, sort-key
/// translation, paging and the item-count aggregation.
/// </summary>
public interface IStockTransferRepository : IRepository<AppStockTransfer, Guid>
{
    Task<AppStockTransfer> GetWithItemsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<long> CountFilteredAsync(
        string? status,
        Guid? fromBranchId,
        Guid? toBranchId,
        string? filter,
        CancellationToken cancellationToken = default);

    Task<List<StockTransferListRow>> GetFilteredListAsync(
        string? status,
        Guid? fromBranchId,
        Guid? toBranchId,
        string? filter,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);
}
