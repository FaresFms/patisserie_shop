using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Production.Orders;

public interface IProductionOrderAppService : IApplicationService
{
    Task<ProductionOrderDto> GetAsync(Guid id);
    Task<PagedResultDto<ProductionOrderListItemDto>> GetListAsync(GetProductionOrdersInput input);
    Task<List<ProductionOrderListItemDto>> GetQualityQueueAsync(Guid? kitchenBranchId = null);
    Task<List<ProductionOrderDto>> CreateFromPlanAsync(Guid planId);
    Task<ProductionOrderDto> RefreshAvailabilityAsync(Guid id);
    Task<ProductionOrderDto> ScheduleAsync(Guid id, ScheduleProductionOrderDto input);
    Task<CreateSubProductionOrdersResultDto> CreateSubProductionOrdersAsync(Guid id);
    Task<CreateIngredientPurchaseOrdersResultDto> CreateDraftIngredientPurchaseOrdersAsync(Guid id);
    Task<ProductionOrderDto> StartAsync(Guid id);
    Task<ProductionOrderDto> CompleteAsync(Guid id, CompleteProductionOrderDto input);
    Task<ProductionOrderDto> HoldQualityAsync(Guid id, ProductionQualityActionDto input);
    Task<ProductionOrderDto> ReleaseQualityAsync(Guid id, ProductionQualityActionDto input);
    Task<ProductionOrderDto> RejectQualityAsync(Guid id, ProductionQualityActionDto input);
    Task<ProductionOrderDto> CancelAsync(Guid id);
}
