using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Inventory.StockBatches;

public interface IStockBatchAppService : IApplicationService
{
    Task<PagedResultDto<StockBatchDto>> GetListAsync(GetStockBatchesInput input);
}
