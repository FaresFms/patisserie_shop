using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Production.Entities;
using Production.Reports;
using Volo.Abp.Domain.Repositories;

namespace Production.Orders;

public interface IProductionOrderRepository : IRepository<AppProductionOrder, Guid>
{
    Task<AppProductionOrder> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AppProductionOrder?> FindByPlanLineAsync(
        Guid productionPlanLineId,
        CancellationToken cancellationToken = default);

    Task<AppProductionOrder?> FindByStockTransferIdAsync(
        Guid stockTransferId,
        CancellationToken cancellationToken = default);

    Task<List<AppProductionOrder>> GetChildrenAsync(
        Guid parentProductionOrderId,
        Guid kitchenBranchId,
        CancellationToken cancellationToken = default);

    Task<bool> HasOrdersForPlanAsync(
        Guid productionPlanId,
        CancellationToken cancellationToken = default);

    Task<List<string>> GetStatusesForPlanAsync(
        Guid productionPlanId,
        CancellationToken cancellationToken = default);

    Task<long> CountFilteredAsync(
        string? filter,
        string? status,
        Guid? kitchenBranchId,
        IReadOnlyCollection<Guid> scopedKitchenBranchIds,
        CancellationToken cancellationToken = default);

    Task<List<ProductionOrderListItem>> GetFilteredListAsync(
        string? filter,
        string? status,
        Guid? kitchenBranchId,
        IReadOnlyCollection<Guid> scopedKitchenBranchIds,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    Task<List<ProductionOrderListItem>> GetQualityQueueAsync(
        Guid? kitchenBranchId,
        IReadOnlyCollection<Guid> scopedKitchenBranchIds,
        CancellationToken cancellationToken = default);

    Task<int> CountOverlappingSchedulesAsync(
        Guid kitchenBranchId,
        string workCenterCode,
        DateTime scheduledStart,
        DateTime scheduledEnd,
        Guid excludeOrderId,
        CancellationToken cancellationToken = default);

    Task<ProductionDashboardReadModel> GetDashboardAsync(
        Guid? kitchenBranchId,
        IReadOnlyCollection<Guid> scopedKitchenBranchIds,
        CancellationToken cancellationToken = default);

    Task<ProductionAnalyticsReadModel> GetAnalyticsAsync(
        int days,
        Guid? kitchenBranchId,
        IReadOnlyCollection<Guid> scopedKitchenBranchIds,
        CancellationToken cancellationToken = default);
}
