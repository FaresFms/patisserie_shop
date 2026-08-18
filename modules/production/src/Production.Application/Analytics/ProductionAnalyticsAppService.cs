using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Operations.Sales;
using Production.Control;
using Production.Kitchens;
using Production.Orders;
using Production.Permissions;
using Production.Plans;
using Production.Reports;
using Volo.Abp.Settings;

namespace Production.Analytics;

[Authorize(ProductionPermissions.Analytics.Default)]
public class ProductionAnalyticsAppService : ProductionAppService, IProductionAnalyticsAppService
{
    private readonly IProductionOrderRepository _orderRepository;
    private readonly KitchenAccessChecker _kitchenAccessChecker;
    private readonly IProductionPlanRepository _planRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly ISettingProvider _settingProvider;

    public ProductionAnalyticsAppService(
        IProductionOrderRepository orderRepository,
        KitchenAccessChecker kitchenAccessChecker,
        IProductionPlanRepository planRepository,
        ISaleRepository saleRepository,
        ISettingProvider settingProvider)
    {
        _orderRepository = orderRepository;
        _kitchenAccessChecker = kitchenAccessChecker;
        _planRepository = planRepository;
        _saleRepository = saleRepository;
        _settingProvider = settingProvider;
    }

    public async Task<ProductionAnalyticsDto> GetAsync(GetProductionAnalyticsInput input)
    {
        if (input.KitchenBranchId.HasValue)
        {
            await _kitchenAccessChecker.EnsureAccessAsync(input.KitchenBranchId.Value);
        }

        var kitchenIds = await _kitchenAccessChecker.GetAccessibleKitchenIdsAsync();
        var model = await _orderRepository.GetAnalyticsAsync(input.Days, input.KitchenBranchId, kitchenIds);

        var profile = await GetControlProfileAsync();
        var windowDays = Math.Clamp(profile.ForecastAccuracyWindowDays, 1, 365);
        var toExclusive = DateTime.UtcNow.Date.AddDays(1);
        var fromInclusive = toExclusive.AddDays(-windowDays);
        var forecasts = await _planRepository.GetForecastSnapshotsAsync(
            fromInclusive,
            toExclusive,
            input.KitchenBranchId,
            kitchenIds);
        var sales = await _saleRepository.GetProductSalesTotalsAsync(
            fromInclusive,
            toExclusive,
            branchIdScope: null);
        var accuracy = ForecastAccuracyCalculator.Calculate(
            forecasts,
            ProductionReportMapper.MapActualDemand(sales));
        model.ForecastQuantity = accuracy.ForecastQuantity;
        model.ActualDemandQuantity = accuracy.ActualQuantity;
        model.ForecastAccuracyPercent = accuracy.AccuracyPercent;
        model.ForecastBiasPercent = accuracy.BiasPercent;
        model.ForecastAccuracy = accuracy.Rows;
        var dto = ProductionReportMapper.MapAnalytics(model);
        dto.ForecastAccuracyDays = windowDays;
        dto.HasForecastAccuracyData = accuracy.Rows.Count > 0;
        return dto;
    }

    private async Task<ProductionControlProfileDto> GetControlProfileAsync()
    {
        var json = await _settingProvider.GetOrNullAsync(ProductionControlSettings.Profile);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ProductionControlProfileDto();
        }

        try
        {
            return JsonSerializer.Deserialize<ProductionControlProfileDto>(
                       json,
                       new JsonSerializerOptions(JsonSerializerDefaults.Web))
                   ?? new ProductionControlProfileDto();
        }
        catch (JsonException)
        {
            return new ProductionControlProfileDto();
        }
    }
}
