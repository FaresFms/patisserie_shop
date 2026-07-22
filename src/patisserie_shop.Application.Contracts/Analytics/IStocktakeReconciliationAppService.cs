using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace patisserie_shop.Analytics;

public interface IStocktakeReconciliationAppService : IApplicationService
{
    Task<StocktakeReconciliationDto> GetAsync(GetStocktakeReconciliationInput input);
}
