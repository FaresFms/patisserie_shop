using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace patisserie_shop.Dashboard;

public interface IAdminDashboardAppService : IApplicationService
{
    Task<AdminDashboardDto> GetAdminDashboardAsync();
}
