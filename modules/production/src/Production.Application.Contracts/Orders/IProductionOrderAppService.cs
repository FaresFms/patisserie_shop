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
    Task<List<ProductionOrderDto>> CreateFromPlanAsync(Guid planId);
    Task<ProductionOrderDto> RefreshAvailabilityAsync(Guid id);
    Task<CreateIngredientPurchaseOrdersResultDto> CreateDraftIngredientPurchaseOrdersAsync(Guid id);
    Task<ProductionOrderDto> StartAsync(Guid id);
    Task<ProductionOrderDto> CompleteAsync(Guid id, CompleteProductionOrderDto input);
    Task<ProductionOrderDto> CancelAsync(Guid id);
}
