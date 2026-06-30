using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Production.Decisions;
using Production.Orders;
using Production.Permissions;

namespace Production.Dashboard;

[Authorize(ProductionPermissions.Dashboard.Default)]
public class ProductionDashboardAppService : ProductionAppService, IProductionDashboardAppService
{
    private readonly IProductionOrderRepository _orderRepository;
    private readonly ProductionDecisionScannerService _decisionScanner;

    public ProductionDashboardAppService(
        IProductionOrderRepository orderRepository,
        ProductionDecisionScannerService decisionScanner)
    {
        _orderRepository = orderRepository;
        _decisionScanner = decisionScanner;
    }

    public async Task<ProductionDashboardDto> GetAsync(GetProductionDashboardInput input)
    {
        var model = await _orderRepository.GetDashboardAsync(input.KitchenBranchId);
        await _decisionScanner.ScanDashboardAsync(model, input.KitchenBranchId);
        return ProductionReportMapper.MapDashboard(model);
    }
}
