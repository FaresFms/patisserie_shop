using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Inventory.StockMovements;

public interface IStockMovementAppService : IApplicationService
{
    Task<PagedResultDto<StockMovementDto>> GetListAsync(GetStockMovementsInput input);

    Task<StockMovementSummaryDto> GetSummaryAsync(GetStockMovementsInput input);

    Task<StockMovementDto> GetAsync(Guid id);
}
