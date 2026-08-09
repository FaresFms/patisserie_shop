using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Production.Kitchens;
using Production.Orders;
using Production.Permissions;

namespace Production.Dashboard;

[Authorize(ProductionPermissions.Dashboard.Default)]
public class ProductionDashboardAppService : ProductionAppService, IProductionDashboardAppService
{
    private readonly IProductionOrderRepository _orderRepository;
    private readonly KitchenAccessChecker _kitchenAccessChecker;

    public ProductionDashboardAppService(
        IProductionOrderRepository orderRepository,
        KitchenAccessChecker kitchenAccessChecker)
    {
        _orderRepository = orderRepository;
        _kitchenAccessChecker = kitchenAccessChecker;
    }

    public async Task<ProductionDashboardDto> GetAsync(GetProductionDashboardInput input)
    {
        if (input.KitchenBranchId.HasValue)
        {
            await _kitchenAccessChecker.EnsureAccessAsync(input.KitchenBranchId.Value);
        }

        var kitchenIds = await _kitchenAccessChecker.GetAccessibleKitchenIdsAsync();
        var model = await _orderRepository.GetDashboardAsync(input.KitchenBranchId, kitchenIds);
        return ProductionReportMapper.MapDashboard(model, L);
    }
}
