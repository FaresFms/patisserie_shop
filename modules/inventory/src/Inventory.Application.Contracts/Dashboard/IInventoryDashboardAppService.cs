using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Inventory.Dashboard;

public interface IInventoryDashboardAppService : IApplicationService
{
    Task<InventoryDashboardDto> GetAsync();
}
