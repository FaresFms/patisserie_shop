using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Production.Entities;
using Volo.Abp.Domain.Repositories;

namespace Production.Plans;

public interface IProductionPlanRepository : IRepository<AppProductionPlan, Guid>
{
    Task<AppProductionPlan> GetWithLinesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<long> CountFilteredAsync(
        string? filter,
        string? status,
        Guid? kitchenBranchId,
        IReadOnlyCollection<Guid> scopedKitchenBranchIds,
        CancellationToken cancellationToken = default);

    Task<List<ProductionPlanListItem>> GetFilteredListAsync(
        string? filter,
        string? status,
        Guid? kitchenBranchId,
        IReadOnlyCollection<Guid> scopedKitchenBranchIds,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    Task<List<ProductionPlanSuggestion>> BuildSuggestionsAsync(
        Guid kitchenBranchId,
        DateTime productionDate,
        decimal forecastSafetyPercent,
        CancellationToken cancellationToken = default);

    Task<List<ProductionForecastSnapshot>> GetForecastSnapshotsAsync(
        DateTime fromInclusive,
        DateTime toExclusive,
        Guid? kitchenBranchId,
        IReadOnlyCollection<Guid> scopedKitchenBranchIds,
        CancellationToken cancellationToken = default);
}
