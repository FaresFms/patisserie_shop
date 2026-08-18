using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Production.Entities;
using Volo.Abp.Domain.Repositories;

namespace Production.Waste;

public interface IProductionWasteRepository : IRepository<AppProductionWaste, Guid>
{
    Task<long> CountFilteredAsync(
        string? filter,
        string? wasteType,
        Guid? kitchenBranchId,
        DateTime? fromDate,
        DateTime? toDate,
        IReadOnlyCollection<Guid> scopedKitchenBranchIds,
        CancellationToken cancellationToken = default);

    Task<List<ProductionWasteListItem>> GetFilteredListAsync(
        string? filter,
        string? wasteType,
        Guid? kitchenBranchId,
        DateTime? fromDate,
        DateTime? toDate,
        IReadOnlyCollection<Guid> scopedKitchenBranchIds,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    Task<ProductionWasteAnalyticsReadModel> GetAnalyticsAsync(
        int days,
        Guid? kitchenBranchId,
        IReadOnlyCollection<Guid> scopedKitchenBranchIds,
        CancellationToken cancellationToken = default);
}
