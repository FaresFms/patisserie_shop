using System;
using System.Collections.Generic;
using Production.Plans;
using Production.Reports;
using Shouldly;
using Xunit;

namespace patisserie_shop.Production;

public class ForecastAccuracyCalculatorTests
{
    [Fact]
    public void Calculate_reports_weighted_accuracy_and_directional_bias()
    {
        var cakeId = Guid.NewGuid();
        var tartId = Guid.NewGuid();
        var result = ForecastAccuracyCalculator.Calculate(
            new List<ProductionForecastSnapshot>
            {
                new() { ProductId = cakeId, ProductName = "Cake", ProductSku = "CAKE", ForecastQuantity = 120 },
                new() { ProductId = tartId, ProductName = "Tart", ProductSku = "TART", ForecastQuantity = 40 }
            },
            new Dictionary<Guid, int>
            {
                [cakeId] = 100,
                [tartId] = 50
            });

        result.ForecastQuantity.ShouldBe(160);
        result.ActualQuantity.ShouldBe(150);
        result.AccuracyPercent.ShouldBe(80m);
        result.BiasPercent.ShouldBe(6.67m);
        result.Rows.Count.ShouldBe(2);
        result.Rows.Find(row => row.ProductId == cakeId)!.AccuracyPercent.ShouldBe(80m);
        result.Rows.Find(row => row.ProductId == tartId)!.BiasPercent.ShouldBe(-20m);
    }

    [Fact]
    public void Calculate_handles_no_forecast_and_no_actual_demand()
    {
        var result = ForecastAccuracyCalculator.Calculate(
            Array.Empty<ProductionForecastSnapshot>(),
            new Dictionary<Guid, int>());

        result.ForecastQuantity.ShouldBe(0);
        result.ActualQuantity.ShouldBe(0);
        result.AccuracyPercent.ShouldBe(100m);
        result.BiasPercent.ShouldBe(0m);
        result.Rows.ShouldBeEmpty();
    }

    [Fact]
    public void Calculate_ignores_request_driven_rows_with_zero_forecast()
    {
        var productId = Guid.NewGuid();
        var result = ForecastAccuracyCalculator.Calculate(
            new List<ProductionForecastSnapshot>
            {
                new() { ProductId = productId, ProductName = "Cake", ProductSku = "CAKE", ForecastQuantity = 0 }
            },
            new Dictionary<Guid, int> { [productId] = 25 });

        result.Rows.ShouldBeEmpty();
        result.ForecastQuantity.ShouldBe(0);
        result.ActualQuantity.ShouldBe(0);
    }
}
