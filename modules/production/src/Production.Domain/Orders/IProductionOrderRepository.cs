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

    Task<long> CountFilteredAsync(
        string? filter,
        string? status,
        Guid? kitchenBranchId,
        CancellationToken cancellationToken = default);

    Task<List<ProductionOrderListItem>> GetFilteredListAsync(
        string? filter,
        string? status,
        Guid? kitchenBranchId,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    Task<ProductionDashboardReadModel> GetDashboardAsync(
        Guid? kitchenBranchId,
        CancellationToken cancellationToken = default);

    Task<ProductionAnalyticsReadModel> GetAnalyticsAsync(
        int days,
        Guid? kitchenBranchId,
        CancellationToken cancellationToken = default);
}
