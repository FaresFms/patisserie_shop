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
