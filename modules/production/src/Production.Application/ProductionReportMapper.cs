using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Localization;
using Operations.Sales;
using Production.Analytics;
using Production.Dashboard;
using Production.Reports;

namespace Production;

internal static class ProductionReportMapper
{
    public static IReadOnlyDictionary<Guid, int> MapActualDemand(
        IReadOnlyCollection<ProductSalesAggregate> rows) =>
        rows.ToDictionary(row => row.ProductId, row => row.TotalQuantitySold);

    public static ProductionDashboardDto MapDashboard(
        ProductionDashboardReadModel model,
        IStringLocalizer localizer)
    {
        var dto = new ProductionDashboardDto
        {
            PendingRequests = model.PendingRequests,
            UnfulfilledDueToday = model.UnfulfilledDueToday,
            WaitingForIngredients = model.WaitingForIngredients,
            ReadyToCook = model.ReadyToCook,
            InProduction = model.InProduction,
            CompletedToday = model.CompletedToday,
            AcceptedToday = model.AcceptedToday,
            RejectedToday = model.RejectedToday,
            WasteCostToday = model.WasteCostToday,
            YieldPercentToday = model.YieldPercentToday,
            FulfillmentPercent = model.FulfillmentPercent
        };

        foreach (var item in model.ActionItems)
        {
            dto.ActionItems.Add(new ProductionActionItemDto
            {
                Type = item.Type,
                Severity = item.Severity,
                Title = localizer[$"DashboardAction:{item.Type}:Title"],
                Detail = localizer[$"DashboardAction:{item.Type}:Detail"],
                Url = item.Url,
                Count = item.Count
            });
        }

        foreach (var point in model.OutputLast7Days)
        {
            dto.OutputLast7Days.Add(new ProductionDailyOutputPointDto
            {
                Date = point.Date,
                AcceptedQuantity = point.AcceptedQuantity,
                RejectedQuantity = point.RejectedQuantity,
                TotalCost = point.TotalCost
            });
        }

        foreach (var product in model.ProductFocus)
        {
            dto.ProductFocus.Add(MapProduct(product));
        }

        return dto;
    }

    public static ProductionAnalyticsDto MapAnalytics(ProductionAnalyticsReadModel model)
    {
        var dto = new ProductionAnalyticsDto
        {
            Days = model.Days,
            PlannedQuantity = model.PlannedQuantity,
            AcceptedQuantity = model.AcceptedQuantity,
            RejectedQuantity = model.RejectedQuantity,
            ApprovedRequestQuantity = model.ApprovedRequestQuantity,
            FulfilledRequestQuantity = model.FulfilledRequestQuantity,
            YieldPercent = model.YieldPercent,
            WastePercent = model.WastePercent,
            FulfillmentPercent = model.FulfillmentPercent,
            PlannedCost = model.PlannedCost,
            ActualCost = model.ActualCost,
            CostVariance = model.CostVariance,
            CostVariancePercent = model.CostVariancePercent,
            AverageUnitCost = model.AverageUnitCost,
            ForecastQuantity = model.ForecastQuantity,
            ActualDemandQuantity = model.ActualDemandQuantity,
            ForecastAccuracyPercent = model.ForecastAccuracyPercent,
            ForecastBiasPercent = model.ForecastBiasPercent
        };

        foreach (var point in model.DailyOutput)
        {
            dto.DailyOutput.Add(new ProductionDailyOutputPointDto
            {
                Date = point.Date,
                AcceptedQuantity = point.AcceptedQuantity,
                RejectedQuantity = point.RejectedQuantity,
                TotalCost = point.TotalCost
            });
        }

        foreach (var product in model.ProductPerformance)
        {
            dto.ProductPerformance.Add(MapProduct(product));
        }

        foreach (var reason in model.WasteReasons)
        {
            dto.WasteReasons.Add(new ProductionWasteReasonAnalyticsRowDto
            {
                Reason = reason.Reason,
                IncidentCount = reason.IncidentCount,
                Cost = reason.Cost
            });
        }

        foreach (var row in model.ForecastAccuracy)
        {
            dto.ForecastAccuracy.Add(new ProductionForecastAccuracyRowDto
            {
                ProductId = row.ProductId,
                ProductName = row.ProductName,
                ProductSku = row.ProductSku,
                ForecastQuantity = row.ForecastQuantity,
                ActualQuantity = row.ActualQuantity,
                AccuracyPercent = row.AccuracyPercent,
                BiasPercent = row.BiasPercent
            });
        }

        return dto;
    }

    private static ProductionProductFocusRowDto MapProduct(ProductionProductFocusRow product) =>
        new()
        {
            ProductId = product.ProductId,
            ProductName = product.ProductName,
            ProductSku = product.ProductSku,
            AcceptedQuantity = product.AcceptedQuantity,
            RejectedQuantity = product.RejectedQuantity,
            YieldPercent = product.YieldPercent,
            UnitCost = product.UnitCost
        };
}
