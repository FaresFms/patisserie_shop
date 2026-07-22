using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Inventory.Stocktakes;

public interface IStocktakeSessionAppService : IApplicationService
{
    Task<StocktakeSessionDto?> GetActiveAsync(Guid branchId);
    Task<StocktakeSessionDto> GetAsync(Guid id);
    Task<PagedResultDto<StocktakeSessionListDto>> GetListAsync(GetStocktakeSessionsInput input);
    Task<StocktakeSessionDto> StartAsync(StartStocktakeSessionDto input);
    Task<StocktakeSessionDto> SaveDraftAsync(SaveStocktakeSessionDraftDto input);
    Task<StocktakeSessionDto> SubmitAsync(SubmitStocktakeSessionDto input);
    Task<ApproveStocktakeSessionResultDto> ApproveAsync(ReviewStocktakeSessionDto input);
    Task<StocktakeSessionDto> RejectAsync(RejectStocktakeSessionDto input);
    Task CancelAsync(CancelStocktakeSessionDto input);
}
