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
        List<Guid>? fromBranchIdIn = null,
        List<Guid>? toBranchIdIn = null,
        StockTransferVisibilitySpec? visibility = null,
        CancellationToken cancellationToken = default);

    Task<List<StockTransferListRow>> GetFilteredListAsync(
        string? status,
        Guid? fromBranchId,
        Guid? toBranchId,
        string? filter,
        string sorting,
        int skipCount,
        int maxResultCount,
        List<Guid>? fromBranchIdIn = null,
        List<Guid>? toBranchIdIn = null,
        StockTransferVisibilitySpec? visibility = null,
        CancellationToken cancellationToken = default);

    /// <summary>Transfers currently waiting for the given user's action (see <see cref="StockTransferActionSpec"/>).</summary>
    Task<long> CountActionRequiredAsync(
        StockTransferActionSpec spec,
        string? status,
        Guid? fromBranchId,
        Guid? toBranchId,
        string? filter,
        CancellationToken cancellationToken = default);

    Task<List<StockTransferListRow>> GetActionRequiredListAsync(
        StockTransferActionSpec spec,
        string? status,
        Guid? fromBranchId,
        Guid? toBranchId,
        string? filter,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    /// <summary>Per-bucket counts of transfers waiting for the given user's action.</summary>
    Task<StockTransferActionCounts> GetActionCountsAsync(
        StockTransferActionSpec spec,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Active incoming transfers for the given destination branch — i.e. status is
    /// Pending, Approved or InTransit. Ordered by most-recent first. Used by the
    /// BranchManager dashboard.
    /// </summary>
    Task<List<StockTransferListRow>> GetActiveIncomingAsync(
        Guid toBranchId,
        int take,
        CancellationToken cancellationToken = default);
}
