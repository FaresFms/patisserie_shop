using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Production.Analytics;

public interface IProductionAnalyticsAppService : IApplicationService
{
    Task<ProductionAnalyticsDto> GetAsync(GetProductionAnalyticsInput input);
}
