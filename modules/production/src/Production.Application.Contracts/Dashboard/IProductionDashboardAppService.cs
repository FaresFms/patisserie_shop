using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Production.Dashboard;

public interface IProductionDashboardAppService : IApplicationService
{
    Task<ProductionDashboardDto> GetAsync(GetProductionDashboardInput input);
}
