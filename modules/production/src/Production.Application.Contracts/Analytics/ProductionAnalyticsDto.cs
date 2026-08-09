using System.Collections.Generic;
using Production.Dashboard;

namespace Production.Analytics;

public class ProductionAnalyticsDto
{
    public int Days { get; set; }
    public int PlannedQuantity { get; set; }
    public int AcceptedQuantity { get; set; }
    public int RejectedQuantity { get; set; }
    public int ApprovedRequestQuantity { get; set; }
    public int FulfilledRequestQuantity { get; set; }
    public decimal YieldPercent { get; set; }
    public decimal WastePercent { get; set; }
    public decimal FulfillmentPercent { get; set; }
    public decimal PlannedCost { get; set; }
    public decimal ActualCost { get; set; }
    public decimal CostVariance { get; set; }
    public decimal CostVariancePercent { get; set; }
    public decimal AverageUnitCost { get; set; }
    public int ForecastQuantity { get; set; }
    public int ActualDemandQuantity { get; set; }
    public int ForecastAccuracyDays { get; set; }
    public bool HasForecastAccuracyData { get; set; }
    public decimal ForecastAccuracyPercent { get; set; }
    public decimal ForecastBiasPercent { get; set; }
    public List<ProductionDailyOutputPointDto> DailyOutput { get; set; } = new();
    public List<ProductionProductFocusRowDto> ProductPerformance { get; set; } = new();
    public List<ProductionWasteReasonAnalyticsRowDto> WasteReasons { get; set; } = new();
    public List<ProductionForecastAccuracyRowDto> ForecastAccuracy { get; set; } = new();
}

public class ProductionForecastAccuracyRowDto
{
    public System.Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductSku { get; set; } = null!;
    public int ForecastQuantity { get; set; }
    public int ActualQuantity { get; set; }
    public decimal AccuracyPercent { get; set; }
    public decimal BiasPercent { get; set; }
}

public class ProductionWasteReasonAnalyticsRowDto
{
    public string Reason { get; set; } = null!;
    public int IncidentCount { get; set; }
    public decimal Cost { get; set; }
}
