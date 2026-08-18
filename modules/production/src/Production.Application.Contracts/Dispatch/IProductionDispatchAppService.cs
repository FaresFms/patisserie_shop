using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Production.Dispatch;

public interface IProductionDispatchAppService : IApplicationService
{
    Task<List<ProductionStockDispatchQueueItemDto>> GetStockDispatchQueueAsync(
        GetProductionStockDispatchQueueInput input);

    Task<ProductionDispatchResultDto> CreateTransferAsync(CreateProductionDispatchTransferDto input);

    Task<ProductionDispatchResultDto> CreateRequestStockTransferAsync(
        CreateProductionRequestStockTransferDto input);
}
