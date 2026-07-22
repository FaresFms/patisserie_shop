using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Volo.Abp.Domain.Repositories;

namespace Inventory.Stocktakes;

public interface IStocktakeSessionRepository : IRepository<AppStocktakeSession, Guid>
{
    Task<AppStocktakeSession?> FindOpenByBranchAsync(
        Guid branchId,
        CancellationToken cancellationToken = default);

    Task<AppStocktakeSession> GetWithLinesAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<long> CountFilteredAsync(
        Guid branchId,
        string? status,
        CancellationToken cancellationToken = default);

    Task<List<AppStocktakeSession>> GetFilteredListAsync(
        Guid branchId,
        string? status,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    Task<StocktakeReconciliationSummary> GetReconciliationSummaryAsync(
        DateTime fromInclusive,
        DateTime toExclusive,
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default);

    Task<List<StocktakeProductVarianceAggregate>> GetProductVarianceAggregatesAsync(
        DateTime fromInclusive,
        DateTime toExclusive,
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default);

    Task<List<StocktakeReasonAggregate>> GetReasonAggregatesAsync(
        DateTime fromInclusive,
        DateTime toExclusive,
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default);
}
