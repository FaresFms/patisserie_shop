using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Volo.Abp.Domain.Repositories;

namespace Inventory.StockBatches;

public interface IStockBatchRepository : IRepository<AppStockBatch, Guid>
{
    /// <summary>
    /// Batches of a product at a branch with QuantityRemaining &gt; 0, ordered by
    /// ExpiryDate ascending. Candidates for FEFO consumption.
    /// </summary>
    Task<List<AppStockBatch>> GetOpenBatchesAsync(
        Guid branchId,
        Guid productId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// All live batches (QuantityRemaining &gt; 0) of ACTIVE products whose
    /// ExpiryDate is on or before <paramref name="maxExpiryDate"/>, joined with
    /// their product. Backs the ExpiringSoon scanner.
    /// </summary>
    Task<List<StockBatchWithProduct>> GetExpiringWithProductAsync(
        DateTime maxExpiryDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Live batches (QuantityRemaining &gt; 0) of ACTIVE products whose ExpiryDate
    /// falls inside the inclusive <paramref name="fromDate"/>..<paramref name="toDate"/>
    /// window, joined with their product and branch, branch-scoped and capped.
    /// Ordered by ExpiryDate then branch. Backs the reorder calendar's expiry layer
    /// (unlike <see cref="GetExpiringWithProductAsync"/> it carries the branch, takes
    /// a lower bound and respects a branch scope).
    /// </summary>
    Task<List<StockBatchWithDetails>> GetExpiringInWindowAsync(
        DateTime fromDate,
        DateTime toDate,
        IReadOnlyCollection<Guid>? branchIdScope,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Total QuantityRemaining across a product+branch's live batches that are
    /// already expired (ExpiryDate strictly before <paramref name="todayUtc"/> —
    /// the expiry day itself still counts as sellable). Backs the waste write-off
    /// execution path.
    /// </summary>
    Task<int> GetExpiredQuantityAsync(
        Guid branchId,
        Guid productId,
        DateTime todayUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Total expired QuantityRemaining per product for ONE branch, keyed by ProductId.
    /// Products with no expired stock are absent from the map. One query — used by the
    /// branch-inventory page so it doesn't N+1 over <see cref="GetExpiredQuantityAsync"/>.
    /// </summary>
    Task<Dictionary<Guid, int>> GetExpiredQuantitiesByProductAsync(
        Guid branchId,
        DateTime todayUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Total expired QuantityRemaining per (product, branch) across ALL branches, keyed
    /// by the (ProductId, BranchId) pair. Pairs with no expired stock are absent. One
    /// query — backs the nightly stockout sweep's sellable-stock adjustment.
    /// </summary>
    Task<Dictionary<(Guid ProductId, Guid BranchId), int>> GetExpiredQuantitiesByProductBranchAsync(
        DateTime todayUtc,
        CancellationToken cancellationToken = default);

    Task<long> CountWithDetailsAsync(
        string? filter,
        Guid? branchId,
        bool includeDepleted,
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default);

    Task<List<StockBatchWithDetails>> GetListWithDetailsAsync(
        string? filter,
        Guid? branchId,
        bool includeDepleted,
        IReadOnlyCollection<Guid>? branchIdScope,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);
}
