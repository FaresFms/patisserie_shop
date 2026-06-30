using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Production.Decisions;
using Production.Orders;
using Production.Permissions;

namespace Production.Analytics;

[Authorize(ProductionPermissions.Analytics.Default)]
public class ProductionAnalyticsAppService : ProductionAppService, IProductionAnalyticsAppService
{
    private readonly IProductionOrderRepository _orderRepository;
    private readonly ProductionDecisionScannerService _decisionScanner;

    public ProductionAnalyticsAppService(
        IProductionOrderRepository orderRepository,
        ProductionDecisionScannerService decisionScanner)
    {
        _orderRepository = orderRepository;
        _decisionScanner = decisionScanner;
    }

    public async Task<ProductionAnalyticsDto> GetAsync(GetProductionAnalyticsInput input)
    {
        var model = await _orderRepository.GetAnalyticsAsync(input.Days, input.KitchenBranchId);
        await _decisionScanner.ScanAnalyticsAsync(model, input.KitchenBranchId);
        return ProductionReportMapper.MapAnalytics(model);
    }
}
