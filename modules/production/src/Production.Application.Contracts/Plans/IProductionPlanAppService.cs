using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Production.Plans;

public interface IProductionPlanAppService : IApplicationService
{
    Task<ProductionPlanDto> GetAsync(Guid id);
    Task<PagedResultDto<ProductionPlanListItemDto>> GetListAsync(GetProductionPlansInput input);
    Task<ProductionPlanDto> CreateDraftAsync(CreateProductionPlanDto input);
    Task<ProductionPlanDto> UpdateLineAsync(Guid id, Guid lineId, UpdateProductionPlanLineDto input);
    Task<ProductionPlanDto> ConfirmAsync(Guid id);
    Task<ProductionPlanDto> CancelAsync(Guid id);
}
