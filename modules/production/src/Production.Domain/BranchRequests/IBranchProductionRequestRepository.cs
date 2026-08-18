using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Production.Entities;
using Volo.Abp.Domain.Repositories;

namespace Production.BranchRequests;

public interface IBranchProductionRequestRepository : IRepository<AppBranchProductionRequest, Guid>
{
    Task<AppBranchProductionRequest> GetWithItemsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<long> CountFilteredAsync(
        string? filter,
        string? status,
        Guid? branchId,
        IReadOnlyCollection<Guid>? scopedBranchIds,
        CancellationToken cancellationToken = default);

    Task<List<BranchProductionRequestListItem>> GetFilteredListAsync(
        string? filter,
        string? status,
        Guid? branchId,
        IReadOnlyCollection<Guid>? scopedBranchIds,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    Task<List<BranchProductionRequestFulfillmentTarget>> GetFulfillmentTargetsAsync(
        Guid branchId,
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<List<BranchProductionRequestPlanningTarget>> GetPlanningTargetsAsync(
        IReadOnlyCollection<Guid> productIds,
        DateTime neededBefore,
        CancellationToken cancellationToken = default);

    Task<List<BranchProductionRequestStockDispatchTarget>> GetStockDispatchTargetsAsync(
        Guid kitchenBranchId,
        DateTime usableOnDate,
        string? filter = null,
        CancellationToken cancellationToken = default);

    Task<BranchProductionRequestStockDispatchTarget?> FindStockDispatchTargetAsync(
        Guid kitchenBranchId,
        Guid requestId,
        Guid requestItemId,
        DateTime usableOnDate,
        CancellationToken cancellationToken = default);
}
