using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Production.Waste;

public interface IProductionWasteAppService : IApplicationService
{
    Task<PagedResultDto<ProductionWasteDto>> GetListAsync(GetProductionWastesInput input);
    Task<ProductionWasteAnalyticsDto> GetAnalyticsAsync(GetProductionWasteAnalyticsInput input);
    Task<ProductionWasteDto> CreateWriteOffAsync(CreateProductionWasteWriteOffDto input);
}
