using System;
using System.Collections.Generic;
using System.Linq;
using Production.Plans;

namespace Production.Reports;

public static class ForecastAccuracyCalculator
{
    public static ForecastAccuracyResult Calculate(
        IReadOnlyCollection<ProductionForecastSnapshot> forecasts,
        IReadOnlyDictionary<Guid, int> actualSales)
    {
        var labels = forecasts
            .Where(row => row.ForecastQuantity > 0)
            .ToDictionary(row => row.ProductId);
        var productIds = labels.Keys.ToList();
        var rows = new List<ForecastAccuracyRow>(productIds.Count);

        foreach (var productId in productIds)
        {
            labels.TryGetValue(productId, out var label);
            var forecast = label?.ForecastQuantity ?? 0;
            var actual = actualSales.GetValueOrDefault(productId);
            var error = forecast - actual;
            rows.Add(new ForecastAccuracyRow
            {
                ProductId = productId,
                ProductName = label?.ProductName ?? productId.ToString(),
                ProductSku = label?.ProductSku ?? string.Empty,
                ForecastQuantity = forecast,
                ActualQuantity = actual,
                AccuracyPercent = Accuracy(forecast, actual),
                BiasPercent = actual == 0
                    ? (forecast == 0 ? 0m : 100m)
                    : Math.Round(error / (decimal)actual * 100m, 2)
            });
        }

        var totalForecast = rows.Sum(row => row.ForecastQuantity);
        var totalActual = rows.Sum(row => row.ActualQuantity);
        var absoluteError = rows.Sum(row => Math.Abs(row.ForecastQuantity - row.ActualQuantity));
        return new ForecastAccuracyResult
        {
            ForecastQuantity = totalForecast,
            ActualQuantity = totalActual,
            AccuracyPercent = totalActual == 0
                ? (totalForecast == 0 ? 100m : 0m)
                : Math.Clamp(Math.Round(100m - absoluteError / (decimal)totalActual * 100m, 2), 0m, 100m),
            BiasPercent = totalActual == 0
                ? (totalForecast == 0 ? 0m : 100m)
                : Math.Round((totalForecast - totalActual) / (decimal)totalActual * 100m, 2),
            Rows = rows.OrderBy(row => row.AccuracyPercent).ThenBy(row => row.ProductName).ToList()
        };
    }

    private static decimal Accuracy(int forecast, int actual)
    {
        if (actual == 0)
        {
            return forecast == 0 ? 100m : 0m;
        }
        return Math.Clamp(
            Math.Round(100m - Math.Abs(forecast - actual) / (decimal)actual * 100m, 2),
            0m,
            100m);
    }
}

public class ForecastAccuracyResult
{
    public int ForecastQuantity { get; set; }
    public int ActualQuantity { get; set; }
    public decimal AccuracyPercent { get; set; }
    public decimal BiasPercent { get; set; }
    public List<ForecastAccuracyRow> Rows { get; set; } = new();
}

public class ForecastAccuracyRow
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductSku { get; set; } = null!;
    public int ForecastQuantity { get; set; }
    public int ActualQuantity { get; set; }
    public decimal AccuracyPercent { get; set; }
    public decimal BiasPercent { get; set; }
}
